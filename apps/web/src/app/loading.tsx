import { ProductGridSkeleton } from "@/components/catalog/product-card";
import { Container } from "@/components/section";

export default function Loading() {
  return (
    <div aria-busy="true">
      <span className="sr-only" role="status">
        Loading
      </span>
      <div className="bg-block-coral h-72 w-full" aria-hidden="true" />
      <Container className="py-12">
        <ProductGridSkeleton count={4} />
      </Container>
    </div>
  );
}
