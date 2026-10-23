import { Card, CardContent } from "@fresh/ui";
import type { Metadata } from "next";
import { LoginForm } from "@/components/auth/login-form";
import { Container } from "@/components/section";

export const metadata: Metadata = { title: "Sign in", robots: { index: false } };

export default async function LoginPage(props: PageProps<"/login">) {
  const { next } = await props.searchParams;
  return (
    <Container className="flex justify-center py-12">
      <Card className="w-full max-w-md">
        <CardContent className="flex flex-col gap-6 p-6 sm:p-8">
          <div className="flex flex-col gap-1">
            <h1 className="text-text text-2xl font-bold">Sign in</h1>
            <p className="text-text-muted text-sm">
              Use your mobile number. New here? Your account is created when you verify the code,
              and anything in your cart comes with you.
            </p>
          </div>
          <LoginForm next={typeof next === "string" ? next : null} />
        </CardContent>
      </Card>
    </Container>
  );
}
