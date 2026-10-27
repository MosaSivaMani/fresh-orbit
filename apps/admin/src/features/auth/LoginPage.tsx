import { zodResolver } from "@hookform/resolvers/zod";
import { isApiError } from "@fresh/core";
import {
  AvatarCircle,
  BlockSplitCard,
  Button,
  CardContent,
  CardDescription,
  CardHeader,
  Script,
  Wordmark,
} from "@fresh/ui";
import { AlertCircle, Clock } from "lucide-react";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { Navigate, useLocation, useSearchParams } from "react-router";
import { z } from "zod";
import { useLogin } from "../../api/auth";
import { useSession } from "../../app/services";
import { TextField } from "../../components/form";
import { applyFieldErrors, errorMessage } from "../../lib/errors";

const schema = z.object({
  email: z
    .string()
    .trim()
    .min(1, "Enter your email address.")
    .email("Enter a valid email address."),
  password: z.string().min(1, "Enter your password."),
});

type LoginValues = z.infer<typeof schema>;

// D33: staff login answers only INVALID_CREDENTIALS for a refused sign-in (no locked / disabled hint), and
// 429 RATE_LIMITED with the server's `retryAfterSeconds` when attempts are throttled.
const INVALID_CREDENTIALS_MESSAGE = "The email or password is not correct.";

/** Seconds to wait from a 429: the body's `retryAfterSeconds`, else the `Retry-After` header. */
function retryAfterSeconds(error: unknown): number | null {
  if (!isApiError(error) || error.status !== 429) return null;
  const fromBody = error.problem.retryAfterSeconds;
  const seconds = typeof fromBody === "number" ? fromBody : error.retryAfterSeconds;
  return typeof seconds === "number" && Number.isFinite(seconds) && seconds > 0
    ? Math.ceil(seconds)
    : 0;
}

/** "1 min 05 s" / "40 s": display only; the wait itself comes from the server. */
function formatWait(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  const rest = seconds % 60;
  if (minutes === 0) return `${rest} s`;
  return rest === 0 ? `${minutes} min` : `${minutes} min ${String(rest).padStart(2, "0")} s`;
}

/** Only same-app paths are accepted as the return target. */
function safeNext(next: string | null): string {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.startsWith("/login"))
    return "/";
  return next;
}

export function LoginPage() {
  const { status } = useSession();
  const [params] = useSearchParams();
  const location = useLocation();
  const login = useLogin();
  const [formError, setFormError] = useState<string | null>(null);
  // Seconds left before the server accepts another attempt (null = not throttled).
  const [waitSeconds, setWaitSeconds] = useState<number | null>(null);
  const throttled = waitSeconds !== null && waitSeconds > 0;

  useEffect(() => {
    if (!throttled) return;
    const timer = window.setTimeout(
      () => setWaitSeconds((s) => (s === null || s <= 1 ? null : s - 1)),
      1000,
    );
    return () => window.clearTimeout(timer);
  }, [throttled, waitSeconds]);
  const form = useForm<LoginValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", password: "" },
  });

  const next = safeNext(params.get("next"));
  const expired =
    params.get("reason") === "expired" || (location.state as { expired?: boolean } | null)?.expired;

  if (status === "signed-in") return <Navigate to={next} replace />;

  const onSubmit = form.handleSubmit(async (values) => {
    if (throttled) return;
    setFormError(null);
    setWaitSeconds(null);
    try {
      await login.mutateAsync({
        email: values.email,
        password: values.password,
        deviceLabel: "Admin web",
      });
    } catch (error) {
      const wait = retryAfterSeconds(error);
      if (wait !== null) {
        setWaitSeconds(wait > 0 ? wait : null);
        if (wait === 0) setFormError("Too many sign-in attempts. Wait a moment and try again.");
        return;
      }
      const unmatched = applyFieldErrors(error, form.setError, ["email", "password"]);
      if (isApiError(error) && error.code === "INVALID_CREDENTIALS")
        setFormError(INVALID_CREDENTIALS_MESSAGE);
      else if (!isApiError(error) || Object.keys(error.errors).length === 0)
        setFormError(errorMessage(error));
      else if (unmatched.length > 0) setFormError(unmatched.join(" "));
    }
  });

  return (
    <div className="bg-canvas flex min-h-dvh items-center justify-center p-4">
      {/* Sign-in is a mango block (D88): the staff character in a ringed circle on md and wider. */}
      <BlockSplitCard
        data-state-slot="ADMIN_LOGIN"
        tone="mango"
        aside={
          <>
            <p className="font-display text-4xl leading-[0.98] font-extrabold tracking-[-0.01em] text-balance">
              Today's catch, <Script>packed and on its way.</Script>
            </p>
            <AvatarCircle slot="ADMIN_LOGIN" size="md" priority />
          </>
        }
      >
          <CardHeader>
            <Wordmark markSrc="/brand/mark.png" className="mb-2" />
            <h1 className="font-display fo-doodle pr-[1em] text-2xl leading-tight font-extrabold tracking-[-0.01em] text-balance">
              Sign in to Fresh Orbitt Admin
            </h1>
            <CardDescription>Sign in with your staff email and password.</CardDescription>
          </CardHeader>
          <CardContent>
            <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
              {throttled && (
                <p
                  role="alert"
                  data-code="RATE_LIMITED"
                  className="bg-warning-subtle text-warning-text flex items-start gap-2 rounded-md px-3 py-2 text-sm"
                >
                  <Clock aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
                  <span>
                    Too many sign-in attempts. Try again in{" "}
                    <span data-testid="retry-after">{formatWait(waitSeconds)}</span>.
                  </span>
                </p>
              )}
              {expired && !formError && !throttled && (
                <p
                  role="status"
                  className="bg-info-subtle text-info-text rounded-md px-3 py-2 text-sm"
                >
                  Your session ended. Sign in again to continue.
                </p>
              )}
              {formError && (
                <p
                  role="alert"
                  className="bg-danger-subtle text-danger-text flex items-start gap-2 rounded-md px-3 py-2 text-sm"
                >
                  <AlertCircle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
                  {formError}
                </p>
              )}
              <TextField
                form={form}
                name="email"
                label="Email"
                type="email"
                autoComplete="username"
                required
              />
              <TextField
                form={form}
                name="password"
                label="Password"
                type="password"
                autoComplete="current-password"
                required
              />
              <Button type="submit" loading={login.isPending} disabled={throttled} fullWidth>
                Sign in
              </Button>
            </form>
          </CardContent>
      </BlockSplitCard>
    </div>
  );
}
