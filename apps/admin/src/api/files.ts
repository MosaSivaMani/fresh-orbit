import { apiErrorFromResponse, readBody } from "@fresh/core";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useServices } from "../app/services";

// Files the API serves only to a signed-in staff member (complaint photos W12, invoice PDFs). An <img src> or a
// plain link cannot carry the Bearer token (the admin keeps it in sessionStorage, never in a cookie), so the bytes
// are fetched with the token and shown through an object URL that is revoked when it is no longer shown (AD-3).

/** `/api/v1/admin/…` (as the API returns it), `/admin/…` (relative to the API base) or an absolute URL. */
export function resolveApiUrl(apiBaseUrl: string, path: string): string {
  if (/^https?:\/\//.test(path)) return path;
  const base = apiBaseUrl.replace(/\/+$/, "");
  const prefix = /\/api\/v1$/.test(base) ? "/api/v1" : "";
  if (prefix && path.startsWith(`${prefix}/`)) return `${base.slice(0, -prefix.length)}${path}`;
  return `${base}${path.startsWith("/") ? path : `/${path}`}`;
}

export const fileKeys = {
  file: (path: string) => ["api-file", path] as const,
};

/** The file at `path`, fetched with the staff token; a non-2xx answer is the usual ApiError. */
export function useApiFile(path: string | null) {
  const { http, apiBaseUrl } = useServices();
  return useQuery({
    queryKey: fileKeys.file(path ?? ""),
    queryFn: async ({ signal }) => {
      const response = await http.fetch(resolveApiUrl(apiBaseUrl, path ?? ""), { signal });
      if (!response.ok) throw apiErrorFromResponse(response, await readBody(response));
      return response.blob();
    },
    enabled: path !== null,
    staleTime: 5 * 60_000,
  });
}

/**
 * Downloads the file at `path` only when asked (a list row, D355): fetched with the staff token like
 * {@link useApiFile}, then saved under `fileName` through a short-lived object URL. Errors are the usual ApiError.
 */
export function useDownloadApiFile() {
  const { http, apiBaseUrl } = useServices();
  return useMutation({
    mutationFn: async ({ path, fileName }: { path: string; fileName: string }) => {
      const response = await http.fetch(resolveApiUrl(apiBaseUrl, path));
      if (!response.ok) throw apiErrorFromResponse(response, await readBody(response));
      saveBlob(await response.blob(), fileName);
      return fileName;
    },
  });
}

/** Saves `blob` as `fileName` (the browser's download), then frees the object URL. */
export function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1_000);
}

/** An object URL for `blob` while the caller is mounted; revoked when the blob changes or the caller unmounts. */
export function useObjectUrl(blob: Blob | undefined): string | undefined {
  const [url, setUrl] = useState<string | undefined>(undefined);
  useEffect(() => {
    if (!blob) {
      setUrl(undefined);
      return undefined;
    }
    const created = URL.createObjectURL(blob);
    setUrl(created);
    return () => URL.revokeObjectURL(created);
  }, [blob]);
  return url;
}
