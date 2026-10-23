import { Button, Dialog, DialogContent, EmptyState, FormField, IconButton, Input } from "@fresh/ui";
import { ArrowDown, ArrowUp, ImageOff, Trash2, Upload } from "lucide-react";
import { useRef, useState } from "react";
import { useDeleteImage, useReorderImages, useUploadImage } from "../../api/catalog";
import { type AdminImage, type AdminProduct } from "../../api/types/catalog";
import { Section } from "../../components/page";
import { notifySaved, reportError } from "../../lib/feedback";

/** Photos of one product: upload (alt text required), reorder, delete. The first photo is the cover. */
export function ImagesPanel({
  product,
  onConflict,
}: {
  product: AdminProduct;
  onConflict: () => unknown;
}) {
  const reorder = useReorderImages(product.id);
  const remove = useDeleteImage(product.id);
  const [deleting, setDeleting] = useState<AdminImage | null>(null);
  const images = [...product.images].sort((a, b) => a.sortOrder - b.sortOrder);

  const move = (index: number, by: -1 | 1) => {
    const ids = images.map((i) => i.id);
    const [moved] = ids.splice(index, 1);
    if (moved === undefined) return;
    ids.splice(index + by, 0, moved);
    reorder.mutate(
      { imageIds: ids, version: product.version },
      { onError: (error) => reportError(error, { onConflict, title: "Photos not reordered" }) },
    );
  };

  return (
    <div className="flex flex-col gap-4">
      <UploadForm product={product} />
      <Section title="Photos" description="The first photo is the cover on the website and app.">
        {images.length === 0 ? (
          <EmptyState
            icon={<ImageOff aria-hidden="true" />}
            title="No photos yet"
            description="A product needs at least one photo before it can be published."
          />
        ) : (
          <ol className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {images.map((image, i) => (
              <li
                key={image.id}
                className="border-border flex flex-col gap-2 rounded-md border p-3"
              >
                <img
                  src={image.url}
                  alt={image.altText}
                  className="aspect-[4/3] w-full rounded-md object-cover"
                />
                <p className="text-sm">{image.altText}</p>
                <p className="text-text-muted text-xs">
                  {i === 0 ? "Cover · " : ""}
                  {image.width}×{image.height} px
                </p>
                <div className="flex gap-1">
                  <IconButton
                    label={`Move photo ${i + 1} earlier`}
                    icon={<ArrowUp aria-hidden="true" />}
                    disabled={i === 0 || reorder.isPending}
                    onClick={() => move(i, -1)}
                  />
                  <IconButton
                    label={`Move photo ${i + 1} later`}
                    icon={<ArrowDown aria-hidden="true" />}
                    disabled={i === images.length - 1 || reorder.isPending}
                    onClick={() => move(i, 1)}
                  />
                  <IconButton
                    label={`Delete photo ${i + 1}`}
                    icon={<Trash2 aria-hidden="true" />}
                    onClick={() => setDeleting(image)}
                  />
                </div>
              </li>
            ))}
          </ol>
        )}
      </Section>
      <Dialog open={deleting !== null} onOpenChange={(open) => !open && setDeleting(null)}>
        <DialogContent
          size="sm"
          title="Delete this photo?"
          description={deleting?.altText}
          footer={
            <>
              <Button variant="outline" onClick={() => setDeleting(null)}>
                Cancel
              </Button>
              <Button
                variant="danger"
                loading={remove.isPending}
                onClick={() => {
                  if (!deleting) return;
                  remove.mutate(
                    { imageId: deleting.id, version: product.version },
                    {
                      onSuccess: () => {
                        notifySaved("Photo deleted");
                        setDeleting(null);
                      },
                      onError: (error) => {
                        setDeleting(null);
                        reportError(error, { onConflict, title: "Photo not deleted" });
                      },
                    },
                  );
                }}
              >
                Delete photo
              </Button>
            </>
          }
        />
      </Dialog>
    </div>
  );
}

function UploadForm({ product }: { product: AdminProduct }) {
  const upload = useUploadImage(product.id);
  const fileInput = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [altText, setAltText] = useState("");
  const [errors, setErrors] = useState<{ file?: string; altText?: string }>({});

  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    const next = {
      ...(file ? {} : { file: "Choose a photo to upload." }),
      ...(altText.trim() ? {} : { altText: "Describe the photo for screen readers." }),
    };
    setErrors(next);
    if (!file || !altText.trim()) return;
    upload.mutate(
      { file, altText: altText.trim() },
      {
        onSuccess: () => {
          notifySaved("Photo uploaded");
          setFile(null);
          setAltText("");
          if (fileInput.current) fileInput.current.value = "";
        },
        onError: (error) => {
          reportError(error, { title: "Photo not uploaded" });
        },
      },
    );
  };

  return (
    <Section
      title="Upload a photo"
      description="JPEG, PNG or WebP up to 5 MB; the shortest side must be at least 800 px."
    >
      <form
        noValidate
        onSubmit={submit}
        className="grid grid-cols-1 items-end gap-4 md:grid-cols-[1fr_1fr_auto]"
      >
        <FormField label="Photo" required error={errors.file}>
          <Input
            ref={fileInput}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
        </FormField>
        <FormField
          label="Alt text"
          required
          error={errors.altText}
          hint="e.g. Seer fish steaks on ice"
        >
          <Input value={altText} onChange={(e) => setAltText(e.target.value)} />
        </FormField>
        <Button
          type="submit"
          loading={upload.isPending}
          leadingIcon={<Upload aria-hidden="true" />}
        >
          Upload
        </Button>
      </form>
    </Section>
  );
}
