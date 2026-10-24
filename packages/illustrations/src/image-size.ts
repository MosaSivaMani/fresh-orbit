// Reads the pixel size of a WebP (or PNG) image from its file header. No dependencies.

export interface ImageSize {
  format: "WEBP" | "PNG";
  width: number;
  height: number;
}

const ascii = (bytes: Uint8Array, start: number, length: number): string =>
  String.fromCharCode(...bytes.subarray(start, start + length));

const u16le = (b: Uint8Array, i: number): number => b[i]! | (b[i + 1]! << 8);
const u24le = (b: Uint8Array, i: number): number => b[i]! | (b[i + 1]! << 8) | (b[i + 2]! << 16);
const u32be = (b: Uint8Array, i: number): number =>
  ((b[i]! << 24) >>> 0) + (b[i + 1]! << 16) + (b[i + 2]! << 8) + b[i + 3]!;

/**
 * Returns the width and height stored in the header, or null when the bytes are not a WebP or PNG
 * image we can read.
 */
export function readImageSize(bytes: Uint8Array): ImageSize | null {
  if (bytes.length >= 30 && ascii(bytes, 0, 4) === "RIFF" && ascii(bytes, 8, 4) === "WEBP") {
    const chunk = ascii(bytes, 12, 4);
    if (chunk === "VP8X") {
      // Extended format: 24-bit canvas width-1 and height-1 at offsets 24 and 27.
      return { format: "WEBP", width: u24le(bytes, 24) + 1, height: u24le(bytes, 27) + 1 };
    }
    if (chunk === "VP8 ") {
      // Lossy: frame tag (3 bytes) at 20, start code 9D 01 2A at 23, then 14-bit width and height.
      if (bytes[23] !== 0x9d || bytes[24] !== 0x01 || bytes[25] !== 0x2a) return null;
      return {
        format: "WEBP",
        width: u16le(bytes, 26) & 0x3fff,
        height: u16le(bytes, 28) & 0x3fff,
      };
    }
    if (chunk === "VP8L") {
      // Lossless: signature 0x2F at 20, then 14-bit width-1 and height-1 packed little-endian.
      if (bytes[20] !== 0x2f) return null;
      const b0 = bytes[21]!;
      const b1 = bytes[22]!;
      const b2 = bytes[23]!;
      const b3 = bytes[24]!;
      const width = 1 + (((b1 & 0x3f) << 8) | b0);
      const height = 1 + (((b3 & 0x0f) << 10) | (b2 << 2) | ((b1 & 0xc0) >> 6));
      return { format: "WEBP", width, height };
    }
    return null;
  }
  if (
    bytes.length >= 24 &&
    bytes[0] === 0x89 &&
    ascii(bytes, 1, 3) === "PNG" &&
    ascii(bytes, 12, 4) === "IHDR"
  ) {
    return { format: "PNG", width: u32be(bytes, 16), height: u32be(bytes, 20) };
  }
  return null;
}
