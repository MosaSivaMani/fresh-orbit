import { ProductGridSkeleton } from "@/components/catalog/product-card";
import { Container } from "@/components/section";

export default function Loading() {
  return (
    <div aria-busy="true">
      <span className="sr-only" role="status">
        Loading products
      </span>
      <div className="fo-sky h-52 w-full" aria-hidden="true" />
      <Container className="py-8">
        <ProductGridSkeleton />
      </Container>
    </div>
  );
}
