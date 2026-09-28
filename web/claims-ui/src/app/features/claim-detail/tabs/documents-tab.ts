import { DatePipe } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { Component, ElementRef, OnInit, inject, input, signal, viewChild } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { finalize } from 'rxjs';
import { ClaimsApiService } from '../../../core/api/claims-api.service';
import { CLOCK } from '../../../core/clock';
import { ApiError } from '../../../core/http/api-error';
import { ClaimDetail } from '../../../core/models/claim.models';
import { ClaimDocumentWithUrl } from '../../../core/models/document.models';
import { DOCUMENT_TYPES, DocumentType, enumLabel } from '../../../core/models/enums';
import { NotificationService } from '../../../core/notify/notification.service';
import { EmptyState } from '../../../shared/ui/empty-state';
import { FileSizePipe } from '../../../shared/ui/file-size.pipe';
import { ErrorSummary } from '../../../shared/ui/error-summary';
import { ClaimDetailStore } from '../claim-detail.store';

/** FRS §13: 50 MB (52,428,800 bytes) and the MIME allowlist, by extension (D-42 item 1). */
export const MAX_DOCUMENT_BYTES = 50 * 1024 * 1024;
export const ALLOWED_EXTENSIONS = [
  '.pdf',
  '.jpg',
  '.jpeg',
  '.png',
  '.docx',
  '.xlsx',
  '.txt',
  '.csv',
];

/**
 * A quick check before uploading, so an obviously wrong file fails without a 50 MB round trip. The API
 * is authoritative: it also sniffs the content (DOC-05), which a browser cannot do reliably.
 */
export function precheckDocument(file: Pick<File, 'name' | 'size'>): string | null {
  const dot = file.name.lastIndexOf('.');
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : '';
  if (!ALLOWED_EXTENSIONS.includes(extension)) {
    return `Only ${ALLOWED_EXTENSIONS.join(', ')} files can be uploaded.`;
  }
  if (file.size === 0) {
    return 'The file is empty.';
  }
  if (file.size > MAX_DOCUMENT_BYTES) {
    return 'The file is larger than 50 MB.';
  }
  return null;
}

/**
 * Tab 4 (FRS §11.3): the claim's documents with a download button (a one-hour SAS URL, BR-D-02) and a
 * file-picker upload with a progress bar (multipart POST /claims/{id}/documents).
 */
@Component({
  selector: 'app-documents-tab',
  imports: [
    ReactiveFormsModule,
    DatePipe,
    FileSizePipe,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatProgressBarModule,
    MatTooltipModule,
    EmptyState,
    ErrorSummary,
  ],
  templateUrl: './documents-tab.html',
  styleUrls: ['./tabs.scss', './documents-tab.scss'],
})
export class DocumentsTab implements OnInit {
  private readonly api = inject(ClaimsApiService);
  private readonly notifications = inject(NotificationService);
  private readonly now = inject(CLOCK);
  protected readonly store = inject(ClaimDetailStore);

  readonly claim = input.required<ClaimDetail>();

  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');

  protected readonly columns = ['name', 'type', 'uploadedAt', 'uploadedBy', 'size', 'download'];
  protected readonly documentTypes = DOCUMENT_TYPES;
  protected readonly label = enumLabel;
  protected readonly accept = ALLOWED_EXTENSIONS.join(',');

  protected readonly documentType = new FormControl<DocumentType>('Other', { nonNullable: true });
  /** Upload progress 0–100, or null when no upload is running. */
  protected readonly progress = signal<number | null>(null);
  protected readonly uploadingName = signal<string | null>(null);
  protected readonly errors = signal<string[]>([]);
  protected readonly opening = signal<string | null>(null);

  ngOnInit(): void {
    this.store.reloadDocuments();
  }

  protected pickFile(): void {
    this.fileInput().nativeElement.click();
  }

  protected onFileChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // Choosing the same file again must fire another change event.
    if (!file) {
      return;
    }

    const problem = precheckDocument(file);
    if (problem) {
      this.errors.set([problem]);
      return;
    }

    this.errors.set([]);
    this.progress.set(0);
    this.uploadingName.set(file.name);
    this.api
      .uploadDocument(this.claim().id, file, this.documentType.value, null, crypto.randomUUID())
      .pipe(
        finalize(() => {
          this.progress.set(null);
          this.uploadingName.set(null);
        }),
      )
      .subscribe({
        next: (event) => {
          if (event.type === HttpEventType.UploadProgress && event.total) {
            this.progress.set(Math.round((event.loaded / event.total) * 100));
          } else if (event.type === HttpEventType.Response && event.body) {
            this.notifications.success(`${event.body.documentName} uploaded.`);
            this.store.reloadDocuments();
            this.store.reloadClaim();
          }
        },
        error: (error: unknown) => {
          if (error instanceof ApiError && error.isValidation) {
            this.errors.set(error.messages);
          }
        },
      });
  }

  /**
   * Opens the document in a new tab through its signed URL (BR-D-02: the bytes never pass through the
   * API). The listed URL is used while it is valid; otherwise a fresh one is fetched (D-08). The tab is
   * opened before the request, because browsers block a window.open that follows an async call.
   */
  protected download(document: ClaimDocumentWithUrl): void {
    const stillValid =
      new Date(document.downloadUrlExpiresAt).getTime() - this.now().getTime() > 60_000;
    if (stillValid) {
      window.open(document.downloadUrl, '_blank', 'noopener');
      return;
    }

    const tab = window.open('', '_blank');
    if (tab) {
      tab.opener = null;
    }
    this.opening.set(document.id);
    this.api
      .getDocumentUrl(this.claim().id, document.id)
      .pipe(finalize(() => this.opening.set(null)))
      .subscribe({
        next: (fresh) => {
          if (tab) {
            tab.location.href = fresh.downloadUrl;
          } else {
            window.open(fresh.downloadUrl, '_blank', 'noopener');
          }
        },
        error: () => tab?.close(),
      });
  }
}
