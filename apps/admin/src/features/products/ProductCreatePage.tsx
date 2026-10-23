import { Card, CardContent } from "@fresh/ui";
import { useNavigate } from "react-router";
import { useCreateProduct } from "../../api/catalog";
import { PageHeader } from "../../components/page";
import { notifySaved } from "../../lib/feedback";
import { ProductDetailsForm } from "./ProductDetailsForm";

export function ProductCreatePage() {
  const create = useCreateProduct();
  const navigate = useNavigate();
  return (
    <>
      <PageHeader
        title="New product"
        description="Products start as drafts. Add variants, a default preparation and a photo, then publish."
        backTo="/products"
        backLabel="Products"
      />
      <Card>
        <CardContent className="pt-5">
          <ProductDetailsForm
            product={null}
            submitLabel="Create draft"
            onSubmit={async (body) => {
              const product = await create.mutateAsync(body);
              notifySaved("Draft created", "Now add its variants and photos.");
              void navigate(`/products/${product.id}`, { replace: true });
              return product;
            }}
          />
        </CardContent>
      </Card>
    </>
  );
}
