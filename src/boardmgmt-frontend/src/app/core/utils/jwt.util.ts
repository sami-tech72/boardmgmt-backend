export interface DecodedJwt {
  sub?: string;
  email?: string;
  exp?: number;
  [k: string]: any;
}

export function decodeJwt(token: string | null | undefined): DecodedJwt | null {
  if (!token) return null;
  const parts = token.split('.');
  if (parts.length !== 3) return null;
  try {
    const payload = JSON.parse(atob(parts[1]));
    return payload ?? null;
  } catch {
    return null;
  }
}

export function isExpired(token: string | null | undefined): boolean {
  const d = decodeJwt(token);
  if (!d?.exp) return true;
  const nowSec = Math.floor(Date.now() / 1000);
  return d.exp <= nowSec;
}
