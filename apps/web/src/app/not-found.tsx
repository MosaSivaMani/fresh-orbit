import { Button, CharacterState, Highlight, Script, SkyPanel, Sparks } from "@fresh/ui";
import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = { title: "Page not found", robots: { index: false } };

/**
 * 404 (D181): the harbour sky, the NOT_FOUND character in its ringed circle, a heading with the handwritten words on
 * the yellow brush and sparks, and three ways on.
 */
export default function NotFound() {
  return (
    <SkyPanel>
      <CharacterState
        slot="NOT_FOUND"
        avatarSize="md"
        headingLevel={1}
        className="py-14 sm:py-20"
        title={
          <>
            This page{" "}
            <Highlight>
              <Script>swam away</Script>
            </Highlight>
            <Sparks className="text-accent-coral ml-1 w-8 -translate-y-4" />
          </>
        }
        description="The page you are looking for does not exist or is no longer available."
        actions={
          <>
            <Button asChild size="lg">
              <Link href="/shop">Browse the shop</Link>
            </Button>
            <Button asChild size="lg" variant="outline">
              <Link href="/">Go home</Link>
            </Button>
            <Button asChild size="lg" variant="ghost">
              <Link href="/support">Get help</Link>
            </Button>
          </>
        }
      />
    </SkyPanel>
  );
}
