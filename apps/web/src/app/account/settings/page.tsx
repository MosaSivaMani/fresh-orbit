import type { Metadata } from "next";
import { SettingsView } from "@/components/account/settings-view";

export const metadata: Metadata = { title: "Settings" };

export default function AccountSettingsPage() {
  return <SettingsView />;
}
