"use client";

import { Button, FormField, Input } from "@fresh/ui";
import { useQueryClient } from "@tanstack/react-query";
import { MessageSquareWarning, TimerReset } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { type FormEvent, useEffect, useRef, useState } from "react";
import { browserHttp } from "@/api/browser";
import { requestOtp } from "@/api/endpoints/account";
import { type OtpChallenge } from "@/api/types/auth";
import { signInWithOtp } from "@/auth/session-store";
import { asApiError, describeError, fieldMessage, hasCode } from "@/lib/errors";
import { queryKeys } from "@/lib/query-keys";

const MOBILE = /^[6-9][0-9]{9}$/;
/** When a 429 carries no Retry-After, wait this long before allowing another try. */
const DEFAULT_RETRY_SECONDS = 30;

/** Only same-site paths are followed after sign-in (no open redirect). */
export function safeNext(next: string | null | undefined): string {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.startsWith("/\\")) return "/";
  return next;
}

/**
 * Verifying the code creates the account, so the terms and the privacy policy are linked before each button (W-3,
 * D300). They open in a new tab, so the number and the code typed so far stay in this form.
 */
function ConsentNote() {
  return (
    <p className="text-text-muted text-sm">
      By continuing you agree to our{" "}
      <Link href="/support/terms" target="_blank" rel="noopener noreferrer" className={linkClass}>
        Terms
      </Link>{" "}
      and{" "}
      <Link
        href="/support/privacy-policy"
        target="_blank"
        rel="noopener noreferrer"
        className={linkClass}
      >
        Privacy policy
      </Link>
      .
    </p>
  );
}

const linkClass = "text-accent-text font-semibold underline-offset-4 hover:underline";

type Step =
  { kind: "phone" } | { kind: "code"; phone: string; challenge: OtpChallenge; resendAt: number };

/**
 * A refusal that is not about a field: too many attempts (429, wait for Retry-After) or the SMS provider being down
 * (503 SMS_PROVIDER_UNAVAILABLE, try again later with the same number).
 */
type Notice = { kind: "rateLimited"; until: number } | { kind: "smsUnavailable"; message: string };

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

function NoticeBanner({
  notice,
  waitSeconds,
  onRetry,
  pending,
}: {
  notice: Notice;
  waitSeconds: number;
  onRetry: () => void;
  pending: boolean;
}) {
  if (notice.kind === "rateLimited") {
    return (
      <div
        role="alert"
        className="bg-highlight-subtle text-highlight-text flex items-start gap-2.5 rounded-2xl p-3 text-sm"
      >
        <TimerReset aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
        <p>
          {waitSeconds > 0
            ? `Too many attempts. Please wait ${waitSeconds} seconds and try again.`
            : "You can try again now."}
        </p>
      </div>
    );
  }
  return (
    <div
      role="alert"
      className="bg-warning-subtle text-warning-text flex flex-col gap-2 rounded-2xl p-3 text-sm"
    >
      <p className="flex items-start gap-2.5">
        <MessageSquareWarning aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
        <span>
          <span className="block font-bold">We could not send the SMS.</span>
          {notice.message}
        </span>
      </p>
      <Button
        variant="outline"
        size="sm"
        className="self-start"
        loading={pending}
        onClick={onRetry}
      >
        Try sending again
      </Button>
    </div>
  );
}

export function LoginForm({ next }: { next: string | null }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [step, setStep] = useState<Step>({ kind: "phone" });
  const [mobile, setMobile] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [pending, setPending] = useState(false);
  const [sentAt, setSentAt] = useState(0);
  const resendIn = useSecondsUntil(step.kind === "code" ? step.resendAt : null, sentAt);
  const waitIn = useSecondsUntil(notice?.kind === "rateLimited" ? notice.until : null, sentAt);
  const rateLimited = notice?.kind === "rateLimited" && waitIn > 0;
  const codeRef = useRef<HTMLInputElement>(null);
  const challengeId = step.kind === "code" ? step.challenge.challengeId : null;
  useEffect(() => {
    if (challengeId) codeRef.current?.focus();
  }, [challengeId]);

  /** Shows a 429 or a 503 SMS refusal as a notice; returns false for anything else. */
  function showNotice(e: unknown): boolean {
    const apiError = asApiError(e);
    if (apiError?.status === 429) {
      const now = Date.now();
      setSentAt(now);
      setNotice({
        kind: "rateLimited",
        until: now + (apiError.retryAfterSeconds ?? DEFAULT_RETRY_SECONDS) * 1000,
      });
      return true;
    }
    if (apiError?.code === "SMS_PROVIDER_UNAVAILABLE") {
      setNotice({ kind: "smsUnavailable", message: describeError(e) });
      return true;
    }
    return false;
  }

  async function sendCode(phone: string) {
    setPending(true);
    setError(null);
    setNotice(null);
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
      if (!showNotice(e)) setError(fieldMessage(e, "phone") ?? describeError(e));
    } finally {
      setPending(false);
    }
  }

  function phoneFromInput(): string | null {
    const digits = mobile.replace(/\D/g, "");
    if (!MOBILE.test(digits)) {
      setError("Enter your 10-digit mobile number.");
      return null;
    }
    return `+91${digits}`;
  }

  async function onPhoneSubmit(event: FormEvent) {
    event.preventDefault();
    const phone = phoneFromInput();
    if (phone) await sendCode(phone);
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
    setNotice(null);
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
      if (showNotice(e)) {
        // Shown above the form.
      } else if (hasCode(e, "OTP_EXPIRED", "OTP_TOO_MANY_ATTEMPTS")) {
        setError(`${describeError(e)} Request a new code.`);
      } else {
        setError(fieldMessage(e, "code") ?? describeError(e));
      }
      setPending(false);
    }
  }

  const banner = notice && (
    <NoticeBanner
      notice={notice}
      waitSeconds={waitIn}
      pending={pending}
      onRetry={() => {
        const phone = step.kind === "code" ? step.phone : phoneFromInput();
        if (phone) void sendCode(phone);
      }}
    />
  );

  if (step.kind === "phone") {
    return (
      <form onSubmit={(e) => void onPhoneSubmit(e)} noValidate className="flex flex-col gap-5">
        {banner}
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
        <ConsentNote />
        <Button type="submit" size="lg" loading={pending} disabled={rateLimited}>
          Send code
        </Button>
      </form>
    );
  }

  return (
    <form onSubmit={(e) => void onCodeSubmit(e)} noValidate className="flex flex-col gap-5">
      {banner}
      <p className="text-text-muted text-sm">
        Enter the code sent to <span className="text-text font-semibold">{step.phone}</span>.{" "}
        <button
          type="button"
          className="text-accent-text font-semibold underline-offset-4 hover:underline"
          onClick={() => {
            setStep({ kind: "phone" });
            setError(null);
            setNotice(null);
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
          className="text-center text-lg font-bold tracking-[0.4em]"
          ref={codeRef}
        />
      </FormField>
      <ConsentNote />
      <Button type="submit" size="lg" loading={pending} disabled={rateLimited}>
        Verify and sign in
      </Button>
      <div className="text-text-muted text-sm" aria-live="polite">
        {resendIn > 0 ? (
          <span>You can ask for a new code in {resendIn} seconds.</span>
        ) : (
          <button
            type="button"
            className="text-accent-text font-semibold underline-offset-4 hover:underline disabled:opacity-50"
            disabled={pending || rateLimited}
            onClick={() => void sendCode(step.phone)}
          >
            Send a new code
          </button>
        )}
      </div>
    </form>
  );
}
