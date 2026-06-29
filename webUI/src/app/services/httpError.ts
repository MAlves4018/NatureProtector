import { ErrorResponse } from '../types/index';

export class HttpError extends Error {
  status: number;
  title: string;
  message: string;

  constructor(status: number, title: string, message: string) {
    super(message);
    this.status = status;
    this.title = title;
    this.message = message;
  }

  static fromResponseBody(body?: ErrorResponse) {
    return new HttpError(
      body?.status || 500,
      body?.title || 'Internal Server Error',
      body?.message || 'An unexpected error occurred',
    );
  }
}
