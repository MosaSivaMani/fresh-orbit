// Hands a downloaded file to the browser's own "save" (an object URL on a temporary `<a download>`), so the
// customer gets the server's file name in their downloads folder without a second, unauthenticated request.

/** Saves [blob] as [fileName]; the object URL is released once the browser has taken the file. */
export function saveFile(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.rel = "noopener";
  link.style.display = "none";
  document.body.appendChild(link);
  try {
    link.click();
  } finally {
    link.remove();
    // Some browsers read the URL after the click returns; release it a moment later.
    setTimeout(() => URL.revokeObjectURL(url), 30_000);
  }
}
