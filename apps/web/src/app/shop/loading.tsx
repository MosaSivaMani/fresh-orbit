import { ProductGridSkeleton } from "@/components/catalog/product-card";
import { Container } from "@/components/section";

export default function Loading() {
  return (
    <Container className="flex flex-col gap-6 py-8" aria-busy="true">
      <span className="sr-only" role="status">
        Loading products
      </span>
      <div className="bg-muted h-4 w-40 animate-pulse rounded" />
      <div className="bg-muted h-9 w-64 animate-pulse rounded" />
      <ProductGridSkeleton />
    </Container>
  );
}
