import { AvatarCircle } from "@fresh/ui";
import type { Metadata } from "next";
import { LoginForm } from "@/components/auth/login-form";
import { Container } from "@/components/section";
import { Highlight, ThemeBand } from "@/components/theme/theme-band";

export const metadata: Metadata = { title: "Sign in", robots: { index: false } };

/**
 * Sign in with an SMS code (D181): the harbour-sky band, then a white card with the LOGIN_OTP character on its
 * edge, overlapping the band's waves like the home's check bar.
 */
export default async function LoginPage(props: PageProps<"/login">) {
  const { next } = await props.searchParams;
  return (
    <>
      <ThemeBand
        className="pb-10 sm:pb-12"
        title={
          <>
            Welcome <Highlight>back</Highlight>
          </>
        }
        script="Fresh fish is one code away"
        description="Sign in with your mobile number. New here? Your account is created when you verify the code, and anything in your cart comes with you."
      />
      <Container className="-mt-12 flex justify-center pb-12 sm:-mt-16">
        <div className="bg-surface-paper border-border relative flex w-full max-w-md flex-col gap-6 rounded-[24px] border p-6 pt-16 shadow-[0_18px_40px_rgb(40_30_10/0.12)] sm:p-8 sm:pt-16">
          <AvatarCircle
            slot="LOGIN_OTP"
            size="xs"
            className="absolute -top-12 left-1/2 -translate-x-1/2"
          />
          <h2 className="font-display text-text text-center text-2xl font-extrabold">Sign in</h2>
          <LoginForm next={typeof next === "string" ? next : null} />
        </div>
      </Container>
    </>
  );
}
