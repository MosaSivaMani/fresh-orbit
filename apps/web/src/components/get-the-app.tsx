import { AppBand, Script } from "@fresh/ui";
import { getAppStoreLinks } from "@/server/config";

/**
 * The "get the app" band (D95). Store links come from configuration (`FRESH_PLAY_STORE_URL`,
 * `FRESH_APP_STORE_URL`); while none is set the band says the app is on its way instead of linking anywhere.
 */
export function GetTheApp() {
  return (
    <AppBand
      title={
        <>
          For a better experience, <Script tone="accent">get the app</Script>
        </>
      }
      description="Order, follow your delivery and reorder from your phone."
      stores={getAppStoreLinks()}
      fallback="The Fresh Orbitt app is coming soon to Android and iPhone."
      slot="ONBOARDING_DELIVERY"
      slotFocus="45% 25%"
    />
  );
}
