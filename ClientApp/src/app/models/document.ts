export type DocumentType = 'Contract' | 'Offer' | 'Invoice';

export const DOCUMENT_TYPES: { value: DocumentType; label: string }[] = [
  { value: 'Contract', label: 'Sözleşme' },
  { value: 'Offer', label: 'Teklif' },
  { value: 'Invoice', label: 'Fatura' }
];

export interface SearchResultItem {
  id: number;
  title: string;
  documentType: DocumentType;
  uploadedBy: string;
  uploadDate: string;
  filePath: string;
  contentHash?: string;
  score: number;
  matchedOn: string;
}

export interface SearchResponse {
  total: number;
  page: number;
  pageSize: number;
  tookMs: number;
  items: SearchResultItem[];
  suggestions: string[];
}

export interface DuplicateInfo {
  isDuplicate: boolean;
  contentHash?: string;
  existingDocumentId?: number;
  existingTitle?: string;
  existingUploadedBy?: string;
  existingUploadDate?: string;
}

export interface UploadResult {
  success: boolean;
  documentId?: number;
  message?: string;
  duplicate?: DuplicateInfo;
}

export interface SearchParams {
  q?: string;
  type?: DocumentType | '';
  from?: string;
  to?: string;
  uploader?: string;
  page?: number;
  pageSize?: number;
}
