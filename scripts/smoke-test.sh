#!/usr/bin/env bash
# Smoke test of a deployed API: the brief §7.2 demo flow, end to end over HTTP (Phase 7, D-44).
#
#   1. handler.alex creates a claim through FNOL, then moves it Draft → Open
#   2. handler.alex submits a $25,000 Indemnity reserve        → PendingApproval, Supervisor authority (BR-R-01..02)
#   3. handler.alex tries to approve it                          → refused (the backend enforces authority)
#   4. supervisor.casey approves it                              → Approved
#   5. the Hangfire GL job posts it                              → PostingStatus = Posted (JOB-02), polled
#   6. handler.alex uploads a PDF, and its signed download URL returns the bytes (BR-D-01..02)
#   7. the audit log holds every step, all under one claim       (BR-A-01)
#
# Usage:  scripts/smoke-test.sh https://ca-claims-api.<env>.<region>.azurecontainerapps.io
#         API_URL=http://localhost:5080 scripts/smoke-test.sh
# Needs:  bash, curl, jq.  Exit code 0 = every check passed.
#
# It creates real data (one claim per run): run it against the demo deployment, never against anything that matters.

set -euo pipefail

API="${1:-${API_URL:-}}"
API="${API%/}"
if [[ -z "$API" ]]; then
  echo "usage: $0 <api base url>   (or set API_URL)" >&2
  exit 2
fi

GL_TIMEOUT_SECONDS="${GL_TIMEOUT_SECONDS:-120}"
RUN_ID="smoke-$(date -u +%Y%m%dT%H%M%SZ)-$RANDOM"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

step() { printf '\n\033[1m▶ %s\033[0m\n' "$*"; }
ok()   { printf '  \033[32m✔\033[0m %s\n' "$*"; }
fail() { printf '  \033[31m✘ %s\033[0m\n' "$*" >&2; exit 1; }

# call METHOD PATH TOKEN [JSON_BODY] -> body in $WORK/body, status in $STATUS
call() {
  local method="$1" path="$2" token="$3" body="${4:-}"
  local args=(-sS -o "$WORK/body" -w '%{http_code}' -X "$method" "$API$path"
    -H "Authorization: Bearer $token" -H "Accept: application/json")
  if [[ -n "$body" ]]; then
    args+=(-H 'Content-Type: application/json' --data "$body")
  fi
  STATUS="$(curl "${args[@]}")"
}

expect() {
  local want="$1" what="$2"
  if [[ "$STATUS" != "$want" ]]; then
    echo "  response body:" >&2
    jq . "$WORK/body" >&2 2>/dev/null || cat "$WORK/body" >&2
    fail "$what: expected HTTP $want, got $STATUS"
  fi
}

sign_in() {
  local status
  status="$(curl -sS -o "$WORK/token" -w '%{http_code}' -X POST "$API/api/auth/dev-token" \
    -H 'Content-Type: application/json' --data "{\"username\":\"$1\"}")"
  [[ "$status" == 200 ]] || fail "dev token for $1: HTTP $status (is Auth:DevTokensEnabled on?)"
  jq -r .accessToken "$WORK/token"
}

# ---------------------------------------------------------------------------------------------------------------------
step "API at $API (run $RUN_ID)"

# A scaled-to-zero Container App plus a paused serverless database can take a minute or two to answer (D-36).
for attempt in $(seq 1 30); do
  if [[ "$(curl -sS -o /dev/null -w '%{http_code}' --max-time 20 "$API/health/ready" || true)" == 200 ]]; then
    ok "ready (/health/ready) after $attempt attempt(s)"
    break
  fi
  [[ $attempt == 30 ]] && fail "/health/ready did not return 200 within about 5 minutes"
  sleep 10
done

[[ "$(curl -sS -o /dev/null -w '%{http_code}' "$API/swagger/v1/swagger.json")" == 200 ]] || fail "Swagger document is not served"
ok "Swagger is served (/swagger/v1/swagger.json)"

HANDLER="$(sign_in handler.alex)"
SUPERVISOR="$(sign_in supervisor.casey)"
ok "signed in as handler.alex and supervisor.casey (dev tokens, D-16)"

# ---------------------------------------------------------------------------------------------------------------------
step "1. FNOL: create a claim and open it"

call GET "/api/policies/search?q=POL-2025-003001" "$HANDLER"
expect 200 "policy search"
POLICY_ID="$(jq -r '.[] | select(.policyNumber == "POL-2025-003001") | .id' "$WORK/body")"
[[ -n "$POLICY_ID" && "$POLICY_ID" != null ]] || fail "seeded in-force policy POL-2025-003001 not found"

LOSS_DATE="$(date -u -v-2d +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -d '2 days ago' +%Y-%m-%dT%H:%M:%SZ)"
FNOL="$(jq -n --arg policy "$POLICY_ID" --arg loss "$LOSS_DATE" --arg run "$RUN_ID" '{
  policyId: $policy,
  lossDate: $loss,
  lossDescription: ("Smoke test " + $run + ": delivery van rear-ended at a junction."),
  lossLocation: "Ring road, junction 4",
  causeOfLossCode: "COL-VEH-COL",
  estimatedLossAmount: 30000,
  severity: "Standard",
  parties: [{ role: "Claimant", type: "Person", firstName: "Jordan", lastName: "Reyes",
              email: "jordan.reyes@example.com", phone: "+1 555 0100" }],
  riskObjects: [{ assetType: "Vehicle", assetDescription: "2022 Ford Transit",
                  damageDescription: "Rear doors and bumper", assetReference: "WF0XXXTTGXNA00042" }]
}')"
call POST /api/claims "$HANDLER" "$FNOL"
expect 201 "create claim"
CLAIM_ID="$(jq -r .id "$WORK/body")"
CLAIM_NUMBER="$(jq -r .claimNumber "$WORK/body")"
[[ "$CLAIM_NUMBER" =~ ^CLM-[0-9]{4}-[0-9]{7}$ ]] || fail "claim number '$CLAIM_NUMBER' does not match CLM-YYYY-NNNNNNN (BR-C-04)"
ok "created $CLAIM_NUMBER ($CLAIM_ID), status $(jq -r .status "$WORK/body")"

call PUT "/api/claims/$CLAIM_ID/status" "$HANDLER" '{"targetStatus":"Open"}'
expect 200 "Draft → Open"
ok "Draft → Open"

# ---------------------------------------------------------------------------------------------------------------------
step "2. handler submits a reserve over \$10,000"

call POST "/api/claims/$CLAIM_ID/reserves" "$HANDLER" \
  '{"component":"Indemnity","amount":25000,"changeReason":"Smoke test: repair estimate from the approved garage."}'
expect 201 "submit reserve"
TXN_ID="$(jq -r .transaction.id "$WORK/body")"
APPROVAL="$(jq -r .transaction.approvalStatus "$WORK/body")"
AUTHORITY="$(jq -r .transaction.requiredAuthority "$WORK/body")"
[[ "$APPROVAL" == PendingApproval ]] || fail "a \$25,000 reserve should be PendingApproval, got $APPROVAL"
ok "transaction $TXN_ID is $APPROVAL, needs $AUTHORITY"

# ---------------------------------------------------------------------------------------------------------------------
step "3. the handler cannot approve it"

call POST "/api/claims/$CLAIM_ID/reserves/$TXN_ID/approve" "$HANDLER" '{}'
[[ "$STATUS" == 403 || "$STATUS" == 422 ]] || fail "handler approval should be refused, got HTTP $STATUS"
ok "refused with HTTP $STATUS"

# ---------------------------------------------------------------------------------------------------------------------
step "4. supervisor approves it"

call POST "/api/claims/$CLAIM_ID/reserves/$TXN_ID/approve" "$SUPERVISOR" '{}'
expect 200 "supervisor approval"
[[ "$(jq -r .approvalStatus "$WORK/body")" == Approved ]] || fail "expected Approved, got $(jq -r .approvalStatus "$WORK/body")"
ok "Approved by supervisor.casey"

# ---------------------------------------------------------------------------------------------------------------------
step "5. the GL posting job posts it (Hangfire, after commit)"

deadline=$(( $(date +%s) + GL_TIMEOUT_SECONDS ))
POSTING=""
while :; do
  call GET "/api/claims/$CLAIM_ID/reserves" "$HANDLER"
  expect 200 "reserves"
  POSTING="$(jq -r --arg id "$TXN_ID" '.transactions[] | select(.id == $id) | .postingStatus' "$WORK/body")"
  [[ "$POSTING" == Posted ]] && break
  [[ "$POSTING" == Failed ]] && fail "GL posting failed (see GL_POSTING_FAILED in the audit log)"
  (( $(date +%s) >= deadline )) && fail "PostingStatus still '$POSTING' after ${GL_TIMEOUT_SECONDS}s"
  sleep 3
done
ok "PostingStatus = Posted"

# ---------------------------------------------------------------------------------------------------------------------
step "6. upload a document and download it through its signed URL"

PDF="$WORK/police-report.pdf"
printf '%%PDF-1.7\n%% smoke test %s\n%%%%EOF\n' "$RUN_ID" > "$PDF"
STATUS="$(curl -sS -o "$WORK/body" -w '%{http_code}' -X POST "$API/api/claims/$CLAIM_ID/documents" \
  -H "Authorization: Bearer $HANDLER" \
  -F "file=@$PDF;type=application/pdf" -F documentType=PoliceReport -F "notes=Smoke test $RUN_ID")"
expect 201 "document upload"
DOCUMENT_ID="$(jq -r .id "$WORK/body")"
ok "uploaded document $DOCUMENT_ID"

call GET "/api/claims/$CLAIM_ID/documents/$DOCUMENT_ID/url" "$HANDLER"
expect 200 "download URL"
DOWNLOAD_URL="$(jq -r .downloadUrl "$WORK/body")"
# No Authorization header: the URL itself is the credential (SAS in Azure, signed token locally).
curl -sSf -o "$WORK/downloaded.pdf" "$DOWNLOAD_URL" || fail "the signed download URL did not return the file"
cmp -s "$PDF" "$WORK/downloaded.pdf" || fail "downloaded bytes differ from the uploaded file"
ok "signed URL returns the same bytes (host: $(echo "$DOWNLOAD_URL" | sed -E 's#^https?://([^/]+).*#\1#'))"

# ---------------------------------------------------------------------------------------------------------------------
step "7. the audit log shows everything"

call GET "/api/claims/$CLAIM_ID/audit?pageSize=200" "$HANDLER"
expect 200 "audit log"
jq -r '.items[] | "    \(.eventType)"' "$WORK/body" | sort | uniq -c
for event in CLAIM_CREATED STATUS_CHANGED RESERVE_CREATED RESERVE_APPROVED GL_POSTING_SIMULATED DOCUMENT_UPLOADED; do
  jq -e --arg e "$event" 'any(.items[]; .eventType == $e)' "$WORK/body" >/dev/null || fail "no $event entry"
done
ok "CLAIM_CREATED, STATUS_CHANGED, RESERVE_CREATED, RESERVE_APPROVED, GL_POSTING_SIMULATED, DOCUMENT_UPLOADED all present"

printf '\n\033[32mSmoke test passed.\033[0m Claim %s (%s).\n' "$CLAIM_NUMBER" "$CLAIM_ID"
