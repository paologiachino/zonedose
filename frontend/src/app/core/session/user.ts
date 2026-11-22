/** The signed-in user, as returned by GET /api/auth/me (field names as in the API). */
export interface User {
  id: string;
  email: string;
  display_name: string | null;
  status: UserStatus;
  role: UserRole;
}

export type UserStatus = 'pending' | 'active' | 'disabled';
export type UserRole = 'user' | 'admin';
