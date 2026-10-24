import { Container } from "@/components/section";

export default function Loading() {
  return (
    <Container className="flex flex-col gap-8 py-8" aria-busy="true">
      <span className="sr-only" role="status">
        Loading product
      </span>
      <div className="bg-muted h-4 w-56 animate-pulse rounded" />
      <div className="grid gap-6 md:grid-cols-2 md:gap-10">
        <div className="bg-ice-soft aspect-[4/3] animate-pulse rounded-[28px]" />
        <div className="flex flex-col gap-4">
          <div className="bg-muted h-10 w-3/4 animate-pulse rounded" />
          <div className="bg-muted h-6 w-1/3 animate-pulse rounded" />
          <div className="bg-muted h-24 animate-pulse rounded-lg" />
          <div className="bg-muted h-24 animate-pulse rounded-lg" />
          <div className="bg-muted h-12 w-48 animate-pulse rounded-lg" />
        </div>
      </div>
    </Container>
  );
}
