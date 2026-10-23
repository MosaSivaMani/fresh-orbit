import { useMutation, useQuery } from "@tanstack/react-query";
import { useServices } from "../app/services";
import {
  type ChangePasswordRequest,
  type StaffLoginRequest,
  type StaffLoginResponse,
  type StaffProfile,
} from "./types/auth";

// docs/api/README.md §4.1 — staff login, logout, profile, change password.

export const authKeys = { me: ["auth", "me"] as const };

export function useLogin() {
  const { http, session, queryClient } = useServices();
  return useMutation({
    mutationFn: (body: StaffLoginRequest) =>
      http.post<StaffLoginResponse>("/admin/auth/login", body),
    onSuccess: (data) => {
      queryClient.clear();
      session.signIn(
        {
          accessToken: data.accessToken,
          accessTokenExpiresAt: data.accessTokenExpiresAt,
          refreshToken: data.refreshToken,
          refreshTokenExpiresAt: data.refreshTokenExpiresAt,
        },
        data.staff,
      );
    },
  });
}

/** Revokes the refresh token on the server, then forgets the session locally even if the call fails. */
export function useLogout() {
  const { http, session, queryClient } = useServices();
  return useMutation({
    mutationFn: async () => {
      const refreshToken = session.getRefreshToken() as string | null;
      try {
        await http.post("/admin/auth/logout", { refreshToken, allDevices: false });
      } catch {
        // The local sign-out below still happens; an unrevoked token expires on its own (12 h).
      }
    },
    onSettled: () => {
      session.signOut();
      queryClient.clear();
    },
  });
}

export function useMe(enabled = true) {
  const { http, session } = useServices();
  return useQuery({
    queryKey: authKeys.me,
    queryFn: async ({ signal }) => {
      const me = await http.get<StaffProfile>("/admin/auth/me", { signal });
      session.setProfile(me);
      return me;
    },
    enabled,
    staleTime: 5 * 60_000,
  });
}

export function useChangePassword() {
  const { http } = useServices();
  return useMutation({
    mutationFn: (body: ChangePasswordRequest) => http.post<void>("/admin/auth/password", body),
  });
}
