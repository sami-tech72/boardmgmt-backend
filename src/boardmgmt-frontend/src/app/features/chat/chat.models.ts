export type ConversationType = 'Direct' | 'Group' | 'Channel';

export interface MinimalUserDto {
  id: string;
  fullName?: string | null;
  email?: string | null;
}

export interface ConversationListItemDto {
  id: string;
  name: string;
  type: ConversationType;
  isPrivate: boolean;
  unreadCount: number;
  lastMessageAtUtc?: string | null;
}

export interface ConversationDetailDto {
  id: string;
  name: string;
  type: ConversationType;
  isPrivate: boolean;
  members: MinimalUserDto[];
}

export interface ReactionDto {
  emoji: string;
  count: number;
  reactedByMe: boolean;
}

export interface ChatAttachmentDto {
  attachmentId: string;
  fileName: string;
  contentType: string;
  fileSize: number;
}

export interface ChatMessageDto {
  id: string;
  conversationId: string;
  threadRootId?: string | null;
  fromUser: MinimalUserDto;
  bodyHtml: string;
  createdAtUtc: string;
  editedAtUtc?: string | null;
  isDeleted: boolean;
  attachments: ChatAttachmentDto[];
  reactions: ReactionDto[];
  threadReplyCount: number;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
}

/** SignalR event payloads we expect from the server */
export interface MessageChangedEvent {
  conversationId: string;
  threadRootId?: string | null;
  message: ChatMessageDto;
  threadRoot?: ChatMessageDto | null;
}

export type MessageCreatedEvent = MessageChangedEvent;
export type MessageEditedEvent = MessageChangedEvent;
export type MessageDeletedEvent = MessageChangedEvent;
export interface ReactionUpdatedEvent {
  messageId: string;
  conversationId: string;
  threadRootId?: string | null;
  reactions: ReactionDto[];
}
export interface TypingEvent {
  conversationId: string;
  userId: string;
  isTyping: boolean;
}
