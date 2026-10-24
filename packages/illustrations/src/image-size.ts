// Reads the pixel size of a WebP, PNG or JPEG image from its file header. No dependencies.

export interface ImageSize {
  format: "WEBP" | "PNG" | "JPEG";
  width: number;
  height: number;
  /** True when the file can carry transparency (PNG with an alpha channel or tRNS, WebP with alpha). */
  alpha: boolean;
}

const ascii = (bytes: Uint8Array, start: number, length: number): string =>
  String.fromCharCode(...bytes.subarray(start, start + length));

const u16le = (b: Uint8Array, i: number): number => b[i]! | (b[i + 1]! << 8);
const u24le = (b: Uint8Array, i: number): number => b[i]! | (b[i + 1]! << 8) | (b[i + 2]! << 16);
const u32be = (b: Uint8Array, i: number): number =>
  ((b[i]! << 24) >>> 0) + (b[i + 1]! << 16) + (b[i + 2]! << 8) + b[i + 3]!;

const u16be = (b: Uint8Array, i: number): number => (b[i]! << 8) | b[i + 1]!;

/** JPEG start-of-frame markers (C0 to CF except DHT C4, JPG C8 and DAC CC) carry the frame size. */
const isStartOfFrame = (marker: number): boolean =>
  marker >= 0xc0 && marker <= 0xcf && marker !== 0xc4 && marker !== 0xc8 && marker !== 0xcc;

function readJpegSize(bytes: Uint8Array): ImageSize | null {
  // Walk the marker segments after SOI; metadata (EXIF, ICC) can push the frame header far in.
  let i = 2;
  while (i + 9 < bytes.length) {
    if (bytes[i] !== 0xff) return null;
    const marker = bytes[i + 1]!;
    if (marker === 0xff) {
      i += 1;
      continue;
    }
    if (marker === 0xd8 || (marker >= 0xd0 && marker <= 0xd7) || marker === 0x01) {
      i += 2;
      continue;
    }
    if (marker === 0xd9 || marker === 0xda) return null;
    if (isStartOfFrame(marker))
      return {
        format: "JPEG",
        width: u16be(bytes, i + 7),
        height: u16be(bytes, i + 5),
        alpha: false,
      };
    i += 2 + u16be(bytes, i + 2);
  }
  return null;
}

const pngHasTransparencyChunk = (bytes: Uint8Array): boolean => {
  // Chunks follow the 8-byte signature: length (4), type (4), data, CRC (4). tRNS comes before IDAT.
  let i = 8;
  while (i + 8 <= bytes.length) {
    const type = ascii(bytes, i + 4, 4);
    if (type === "tRNS") return true;
    if (type === "IDAT" || type === "IEND") return false;
    i += 12 + u32be(bytes, i);
  }
  return false;
};

/**
 * Returns the size stored in the header, or null when the bytes are not a WebP, PNG or JPEG image we can
 * read. Pass the whole file for JPEG (its frame header may sit after large metadata).
 */
export function readImageSize(bytes: Uint8Array): ImageSize | null {
  if (bytes.length >= 30 && ascii(bytes, 0, 4) === "RIFF" && ascii(bytes, 8, 4) === "WEBP") {
    const chunk = ascii(bytes, 12, 4);
    if (chunk === "VP8X") {
      // Extended format: 24-bit canvas width-1 and height-1 at offsets 24 and 27.
      return {
        format: "WEBP",
        width: u24le(bytes, 24) + 1,
        height: u24le(bytes, 27) + 1,
        alpha: (bytes[20]! & 0x10) !== 0,
      };
    }
    if (chunk === "VP8 ") {
      // Lossy: frame tag (3 bytes) at 20, start code 9D 01 2A at 23, then 14-bit width and height.
      if (bytes[23] !== 0x9d || bytes[24] !== 0x01 || bytes[25] !== 0x2a) return null;
      return {
        format: "WEBP",
        width: u16le(bytes, 26) & 0x3fff,
        height: u16le(bytes, 28) & 0x3fff,
        alpha: false,
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
      return { format: "WEBP", width, height, alpha: ((b3 >> 4) & 0x01) === 1 };
    }
    return null;
  }
  if (
    bytes.length >= 24 &&
    bytes[0] === 0x89 &&
    ascii(bytes, 1, 3) === "PNG" &&
    ascii(bytes, 12, 4) === "IHDR"
  ) {
    const colourType = bytes[25] ?? 0;
    return {
      format: "PNG",
      width: u32be(bytes, 16),
      height: u32be(bytes, 20),
      alpha: colourType === 4 || colourType === 6 || pngHasTransparencyChunk(bytes),
    };
  }
  if (bytes.length >= 4 && bytes[0] === 0xff && bytes[1] === 0xd8 && bytes[2] === 0xff)
    return readJpegSize(bytes);
  return null;
}
