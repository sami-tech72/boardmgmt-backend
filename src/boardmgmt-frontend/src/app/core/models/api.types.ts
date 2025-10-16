export interface ApiResponse<T> {
  success: true;
  data: T;
  message?: string | null;
  traceId: string;
  timestamp: string;
}

export interface ApiError {
  code: string;
  message: string;
  details?: unknown;
}

export interface ApiErrorResponse {
  success: false;
  error: ApiError;
  traceId: string;
  timestamp: string;
}
