import { formatDateTime } from "@fresh/core";
import { Badge, Button } from "@fresh/ui";
import { FileDown } from "lucide-react";
import { Link } from "react-router";
import { useDownloadApiFile } from "../../api/files";
import { useOrderInvoice } from "../../api/orders";
import { type Id } from "../../api/types/common";
import { DetailList, Section } from "../../components/page";
import { reportError } from "../../lib/feedback";

/** The file name the server offers too (D343): the invoice number with `/` turned into `-`. */
function invoicePdfFileName(invoiceNumber: string): string {
  return `${invoiceNumber.replace(/\//g, "-")}.pdf`;
}

/**
 * Downloads the invoice PDF when clicked (D355): fetched with the staff token (a plain link cannot carry it, D303)
 * and saved under the invoice number. Nothing is fetched until the click, so a list of invoices loads no PDFs.
 */
export function InvoicePdfDownload({
  path,
  invoiceNumber,
  size = "sm",
}: {
  /** The server's `pdfUrl`. */
  path: string;
  invoiceNumber: string;
  size?: "sm" | "md";
}) {
  const download = useDownloadApiFile();
  function save() {
    download.mutate(
      { path, fileName: invoicePdfFileName(invoiceNumber) },
      { onError: (error) => reportError(error, { title: "The PDF could not be downloaded" }) },
    );
  }
  return (
    <Button
      variant="outline"
      size={size}
      loading={download.isPending}
      leadingIcon={<FileDown aria-hidden="true" />}
      aria-label={`Download PDF of invoice ${invoiceNumber}`}
      onClick={save}
    >
      PDF
    </Button>
  );
}

/** The invoice exists but its PDF is still being rendered by the server (ORD-R53). */
export function PdfNotReady() {
  return (
    <Badge tone="warning" title="The server is still preparing the PDF; check again in a minute.">
      PDF not ready
    </Badge>
  );
}

/**
 * The order detail's invoice block (AD-5, D356): number and date from the order, "View" opens the printable invoice
 * page, "PDF" downloads the server's file, or "PDF not ready" while it is being rendered. Before delivery there is no
 * invoice yet.
 */
export function OrderInvoiceSection({
  orderId,
  invoice,
}: {
  orderId: Id;
  invoice: { invoiceNumber: string; issuedAt: string } | null | undefined;
}) {
  return (
    <Section title="Invoice">
      {invoice ? (
        <div className="flex flex-col gap-3">
          <DetailList
            columns={1}
            items={[
              { label: "Number", value: invoice.invoiceNumber },
              { label: "Issued", value: formatDateTime(invoice.issuedAt) },
            ]}
          />
          <div className="flex flex-wrap items-center gap-2">
            <Button asChild variant="outline" size="sm">
              <Link to={`/orders/${orderId}/invoice`}>View</Link>
            </Button>
            <OrderInvoicePdf orderId={orderId} invoiceNumber={invoice.invoiceNumber} />
          </div>
        </div>
      ) : (
        <p className="text-text-muted text-sm">
          The invoice is issued when the order is delivered.
        </p>
      )}
    </Section>
  );
}

/** Reads the invoice for its `pdfUrl` (null until the PDF is rendered). */
function OrderInvoicePdf({ orderId, invoiceNumber }: { orderId: Id; invoiceNumber: string }) {
  const invoice = useOrderInvoice(orderId);
  if (invoice.isPending) {
    return <Button variant="outline" size="sm" disabled loading>PDF</Button>;
  }
  if (invoice.isError) {
    return <span className="text-text-muted text-sm">The PDF link could not be loaded.</span>;
  }
  return invoice.data.pdfUrl ? (
    <InvoicePdfDownload path={invoice.data.pdfUrl} invoiceNumber={invoiceNumber} />
  ) : (
    <PdfNotReady />
  );
}
