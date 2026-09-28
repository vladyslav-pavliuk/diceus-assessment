// Mirrors of ClaimsModule.Application/Claims/DocumentDtos.cs.

import { DocumentType } from './enums';

/** DocumentDto: a document with a download URL valid for one hour (BR-D-02). */
export interface ClaimDocumentWithUrl {
  id: string;
  documentType: DocumentType;
  documentName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string | null;
  uploadedByName: string | null;
  notes: string | null;
  downloadUrl: string;
  downloadUrlExpiresAt: string;
}

export interface DocumentDownloadUrl {
  documentId: string;
  downloadUrl: string;
  expiresAt: string;
}
