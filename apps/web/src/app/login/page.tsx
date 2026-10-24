import { AvatarCircle, PageBand, Script } from "@fresh/ui";
import type { Metadata } from "next";
import { LoginForm } from "@/components/auth/login-form";
import { Container } from "@/components/section";

export const metadata: Metadata = { title: "Sign in", robots: { index: false } };

/** Sign in with an SMS code (D95): the coral band, then a white card with the LOGIN_OTP character on its edge. */
export default async function LoginPage(props: PageProps<"/login">) {
  const { next } = await props.searchParams;
  return (
    <>
      <PageBand
        className="pb-16 sm:pb-20"
        title={
          <>
            Welcome <Script>back</Script>
          </>
        }
        description="Sign in with your mobile number. New here? Your account is created when you verify the code, and anything in your cart comes with you."
      />
      <Container className="-mt-14 flex justify-center pb-12 sm:-mt-16">
        <div className="bg-surface-paper border-border relative flex w-full max-w-md flex-col gap-6 rounded-[26px] border p-6 pt-16 shadow-[0_18px_40px_rgb(40_30_10/0.12)] sm:p-8 sm:pt-16">
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
