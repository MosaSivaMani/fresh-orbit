import { zodResolver } from "@hookform/resolvers/zod";
import { isApiError } from "@fresh/core";
import { Button, Card, CardContent, CardHeader, CardTitle, CardDescription } from "@fresh/ui";
import { AlertCircle, Fish } from "lucide-react";
import { useState } from "react";
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

const LOGIN_MESSAGES: Readonly<Record<string, string>> = {
  INVALID_CREDENTIALS: "The email or password is not correct.",
  ACCOUNT_LOCKED:
    "This account is locked after too many failed attempts. Ask an administrator to reset it.",
  ACCOUNT_DISABLED: "This account is disabled. Ask an administrator.",
  RATE_LIMITED: "Too many sign-in attempts. Wait a few minutes and try again.",
};

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
  const form = useForm<LoginValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", password: "" },
  });

  const next = safeNext(params.get("next"));
  const expired =
    params.get("reason") === "expired" || (location.state as { expired?: boolean } | null)?.expired;

  if (status === "signed-in") return <Navigate to={next} replace />;

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await login.mutateAsync({
        email: values.email,
        password: values.password,
        deviceLabel: "Admin web",
      });
    } catch (error) {
      const unmatched = applyFieldErrors(error, form.setError, ["email", "password"]);
      if (isApiError(error) && LOGIN_MESSAGES[error.code])
        setFormError(LOGIN_MESSAGES[error.code] ?? null);
      else if (!isApiError(error) || Object.keys(error.errors).length === 0)
        setFormError(errorMessage(error));
      else if (unmatched.length > 0) setFormError(unmatched.join(" "));
    }
  });

  return (
    <div className="bg-background flex min-h-dvh items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <div className="bg-primary text-on-primary mb-2 flex size-10 items-center justify-center rounded-lg">
            <Fish aria-hidden="true" className="size-5" />
          </div>
          <CardTitle>Sign in to Fresh Orbit Admin</CardTitle>
          <CardDescription>Sign in with your staff email and password.</CardDescription>
        </CardHeader>
        <CardContent>
          <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
            {expired && !formError && (
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
            <Button type="submit" loading={login.isPending} fullWidth>
              Sign in
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
