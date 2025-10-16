export type MessagePriority = 'Low' | 'Normal' | 'High' | 'Urgent';
export type MessageStatus = 'Draft' | 'Sent';

export interface MinimalUserDto {
  id: string;
  fullName?: string | null;
  email?: string | null;
}
export interface MessageAttachmentDto {
  attachmentId: string;
  fileName: string;
  contentType?: string;
  fileSize?: number;
}

export interface MessageListItemDto {
  id: string;
  subject: string;
  preview: string;
  fromUser?: MinimalUserDto | null;
  priority: MessagePriority;
  status: MessageStatus;
  createdAtUtc: string;
  sentAtUtc?: string | null;
  updatedAtUtc: string;
  hasAttachments?: boolean;
}

export interface MessageDetailDto {
  id: string;
  subject: string;
  body: string;
  fromUser?: MinimalUserDto | null;
  recipients: MinimalUserDto[];
  priority: MessagePriority;
  status: MessageStatus;
  createdAtUtc: string;
  sentAtUtc?: string | null;
  updatedAtUtc: string;
  attachments: MessageAttachmentDto[];
}

export interface MessageBubble {
  id: string;
  fromUser: MinimalUserDto;
  body: string;
  createdAtUtc: string;
  attachments: MessageAttachmentDto[];
}
export interface MessageThreadDto {
  anchorMessageId: string;
  subject: string;
  participants: MinimalUserDto[];
  items: MessageBubble[];
}
export interface PagedResult<T> {
  items: T[];
  total: number;
}

export interface CreateMessageRequest {
  subject: string;
  body: string;
  priority: MessagePriority;
  recipientIds: string[];
  readReceiptRequested?: boolean;
  isConfidential?: boolean;
  asDraft?: boolean;
}
