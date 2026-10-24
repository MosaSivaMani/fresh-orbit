import { Button, CharacterState, Script } from "@fresh/ui";
import type { Metadata } from "next";
import Link from "next/link";
import { Container } from "@/components/section";

export const metadata: Metadata = { title: "Page not found", robots: { index: false } };

/** 404 (D95): the NOT_FOUND character in its ringed circle, a heading with a handwritten word, and two ways on. */
export default function NotFound() {
  return (
    <Container>
      <CharacterState
        slot="NOT_FOUND"
        avatarSize="md"
        headingLevel={1}
        className="py-14 sm:py-20"
        title={
          <>
            This page <Script tone="accent">swam away</Script>
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
          </>
        }
      />
    </Container>
  );
}
