"use client";

import { Button, FormField, Input } from "@fresh/ui";
import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { type FormEvent, useEffect, useRef, useState } from "react";
import { browserHttp } from "@/api/browser";
import { requestOtp } from "@/api/endpoints/account";
import { type OtpChallenge } from "@/api/types/auth";
import { signInWithOtp } from "@/auth/session-store";
import { describeError, fieldMessage, hasCode } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

const MOBILE = /^[6-9][0-9]{9}$/;

/** Only same-site paths are followed after sign-in (no open redirect). */
export function safeNext(next: string | null | undefined): string {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.startsWith("/\\")) return "/";
  return next;
}

type Step =
  { kind: "phone" } | { kind: "code"; phone: string; challenge: OtpChallenge; resendAt: number };

/** Seconds until `deadline` (epoch ms), ticking once a second while above zero. */
function useSecondsUntil(deadline: number | null, startedAt: number): number {
  const [now, setNow] = useState(startedAt);
  useEffect(() => {
    if (deadline === null) return;
    const timer = setInterval(() => {
      const current = Date.now();
      setNow(current);
      if (current >= deadline) clearInterval(timer);
    }, 1000);
    return () => clearInterval(timer);
  }, [deadline]);
  return deadline === null
    ? 0
    : Math.max(0, Math.ceil((deadline - Math.max(now, startedAt)) / 1000));
}

export function LoginForm({ next }: { next: string | null }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [step, setStep] = useState<Step>({ kind: "phone" });
  const [mobile, setMobile] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [sentAt, setSentAt] = useState(0);
  const resendIn = useSecondsUntil(step.kind === "code" ? step.resendAt : null, sentAt);
  const codeRef = useRef<HTMLInputElement>(null);
  const challengeId = step.kind === "code" ? step.challenge.challengeId : null;
  useEffect(() => {
    if (challengeId) codeRef.current?.focus();
  }, [challengeId]);

  async function sendCode(phone: string) {
    setPending(true);
    setError(null);
    try {
      const challenge = await requestOtp(browserHttp(), phone);
      const now = Date.now();
      setCode("");
      setSentAt(now);
      setStep({
        kind: "code",
        phone,
        challenge,
        resendAt: now + challenge.resendAfterSeconds * 1000,
      });
    } catch (e) {
      setError(fieldMessage(e, "phone") ?? describeError(e));
    } finally {
      setPending(false);
    }
  }

  async function onPhoneSubmit(event: FormEvent) {
    event.preventDefault();
    const digits = mobile.replace(/\D/g, "");
    if (!MOBILE.test(digits)) {
      setError("Enter your 10-digit mobile number.");
      return;
    }
    await sendCode(`+91${digits}`);
  }

  async function onCodeSubmit(event: FormEvent) {
    event.preventDefault();
    if (step.kind !== "code") return;
    if (!/^[0-9]+$/.test(code) || code.length !== step.challenge.codeLength) {
      setError(`Enter the ${step.challenge.codeLength}-digit code we sent you.`);
      return;
    }
    setPending(true);
    setError(null);
    try {
      await signInWithOtp({
        challengeId: step.challenge.challengeId,
        phone: step.phone,
        code,
      });
      // The guest cart merges in the background (docs/api §4.1); read the cart now and once more shortly after.
      await queryClient.invalidateQueries({ queryKey: queryKeys.cart });
      setTimeout(() => void queryClient.invalidateQueries({ queryKey: queryKeys.cart }), 2500);
      router.replace(safeNext(next));
    } catch (e) {
      if (hasCode(e, "OTP_EXPIRED", "OTP_TOO_MANY_ATTEMPTS")) {
        setError(`${describeError(e)} Request a new code.`);
      } else {
        setError(fieldMessage(e, "code") ?? describeError(e));
      }
      setPending(false);
    }
  }

  if (step.kind === "phone") {
    return (
      <form onSubmit={(e) => void onPhoneSubmit(e)} noValidate className="flex flex-col gap-5">
        <FormField
          label="Mobile number"
          hint="We will send a one-time code by SMS."
          error={error ?? undefined}
        >
          <Input
            type="tel"
            inputMode="numeric"
            autoComplete="tel-national"
            maxLength={10}
            value={mobile}
            onChange={(e) => setMobile(e.target.value.replace(/\D/g, ""))}
            startAdornment={<span className="text-text-muted text-sm">+91</span>}
          />
        </FormField>
        <Button type="submit" size="lg" loading={pending}>
          Send code
        </Button>
      </form>
    );
  }

  return (
    <form onSubmit={(e) => void onCodeSubmit(e)} noValidate className="flex flex-col gap-5">
      <p className="text-text-muted text-sm">
        Enter the code sent to <span className="text-text font-semibold">{step.phone}</span>.{" "}
        <button
          type="button"
          className="text-accent-text font-semibold underline-offset-4 hover:underline"
          onClick={() => {
            setStep({ kind: "phone" });
            setError(null);
          }}
        >
          Change number
        </button>
      </p>
      <FormField label="One-time code" error={error ?? undefined}>
        <Input
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={step.challenge.codeLength}
          value={code}
          onChange={(e) => setCode(e.target.value.replace(/\D/g, ""))}
          className="tracking-[0.4em]"
          ref={codeRef}
        />
      </FormField>
      <Button type="submit" size="lg" loading={pending}>
        Verify and sign in
      </Button>
      <div className="text-text-muted text-sm" aria-live="polite">
        {resendIn > 0 ? (
          <span>You can ask for a new code in {resendIn} seconds.</span>
        ) : (
          <button
            type="button"
            className="text-accent-text font-semibold underline-offset-4 hover:underline disabled:opacity-50"
            disabled={pending}
            onClick={() => void sendCode(step.phone)}
          >
            Send a new code
          </button>
        )}
      </div>
    </form>
  );
}
