import { DocumentType } from './enums';

/** The download URL is valid for one hour (BR-D-02). */
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
