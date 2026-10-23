import type { Metadata } from "next";
import { ProfileView } from "@/components/account/profile-view";

export const metadata: Metadata = { title: "Profile" };

export default function AccountPage() {
  return <ProfileView />;
}
