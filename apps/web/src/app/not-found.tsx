import { Fish } from "lucide-react";
import type { Metadata } from "next";
import Link from "next/link";
import { Container } from "@/components/section";

export const metadata: Metadata = { title: "Page not found", robots: { index: false } };

export default function NotFound() {
  return (
    <Container className="flex flex-col items-center gap-4 py-20 text-center">
      <span className="bg-accent-subtle text-accent-text flex size-14 items-center justify-center rounded-full">
        <Fish aria-hidden="true" className="size-7" />
      </span>
      <h1 className="text-text text-3xl font-bold">This page swam away</h1>
      <p className="text-text-muted max-w-md">
        The page you are looking for does not exist or is no longer available.
      </p>
      <div className="flex flex-wrap justify-center gap-3">
        <Link
          href="/shop"
          className="bg-primary text-on-primary hover:bg-primary-hover inline-flex h-10 items-center rounded-md px-4 text-sm font-medium"
        >
          Browse the shop
        </Link>
        <Link
          href="/"
          className="border-border-strong bg-surface text-text hover:bg-muted inline-flex h-10 items-center rounded-md border px-4 text-sm font-medium"
        >
          Go home
        </Link>
      </div>
    </Container>
  );
}
