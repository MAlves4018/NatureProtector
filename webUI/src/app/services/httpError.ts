import { ErrorResponse } from '../types/index';

export class HttpError extends Error {
  status: number;
  title: string;
  message: string;
  retryAfterSeconds: number | null;

  constructor(status: number, title: string, message: string, retryAfterSeconds: number | null = null) {
    super(message);
    this.status = status;
    this.title = title;
    this.message = message;
    this.retryAfterSeconds = retryAfterSeconds;
  }

  static fromResponseBody(body?: ErrorResponse, retryAfterSeconds: number | null = null) {
    return new HttpError(
      body?.status || 500,
      body?.title || 'Internal Server Error',
      body?.message || 'An unexpected error occurred',
      retryAfterSeconds,
    );
  }
}
