import { formatDateTime, formatPercentFromBp, formatQuantity, formatRupees } from "@fresh/core";
import { Button } from "@fresh/ui";
import { FileDown, Printer } from "lucide-react";
import { useParams } from "react-router";
import { useApiFile, useObjectUrl } from "../../api/files";
import { useOrderInvoice } from "../../api/orders";
import { type Invoice } from "../../api/types/orders";
import { PageHeader, QueryView, Section } from "../../components/page";

/**
 * The order's GST tax invoice (AD-5) from `GET /admin/orders/{id}/invoice`, laid out to print: "Print" prints only
 * the invoice (the `fo-print-area` print styles hide the shell). Every amount is the server's; the page only formats.
 */
export function InvoicePage() {
  const { orderId = "" } = useParams();
  const query = useOrderInvoice(orderId);
  // A 404 (not issued yet) shows the not-found state through QueryView.
  return (
    <QueryView query={query} what="invoice">
      {(invoice) => <InvoiceView invoice={invoice} />}
    </QueryView>
  );
}

function InvoiceView({ invoice }: { invoice: Invoice }) {
  return (
    <>
      <PageHeader
        backTo={`/orders/${invoice.orderId}`}
        backLabel={`Order ${invoice.orderNumber}`}
        title={`Invoice ${invoice.invoiceNumber}`}
        description={`Issued ${formatDateTime(invoice.issuedAt)}`}
        actions={
          <div className="flex flex-wrap gap-2">
            {invoice.pdfUrl && <PdfLink path={invoice.pdfUrl} name={invoice.invoiceNumber} />}
            <Button leadingIcon={<Printer aria-hidden="true" />} onClick={() => window.print()}>
              Print
            </Button>
          </div>
        }
      />
      <div className="fo-print-area">
        <Section title="Tax invoice">
          <div className="mb-4 grid gap-1 text-sm sm:grid-cols-2">
            <div>
              <p className="font-semibold">{invoice.sellerLegalName}</p>
              <p>GSTIN {invoice.sellerGstin}</p>
            </div>
            <div className="sm:text-right">
              <p>
                Invoice <span className="font-semibold">{invoice.invoiceNumber}</span>
              </p>
              <p>Order {invoice.orderNumber}</p>
              <p>{formatDateTime(invoice.issuedAt)}</p>
            </div>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <caption className="sr-only">Invoice lines</caption>
              <thead>
                <tr className="border-border border-b text-left">
                  <th scope="col" className="py-2 pr-2">
                    #
                  </th>
                  <th scope="col" className="py-2 pr-2">
                    Item
                  </th>
                  <th scope="col" className="py-2 pr-2">
                    HSN
                  </th>
                  <th scope="col" className="py-2 pr-2 text-right">
                    Quantity
                  </th>
                  <th scope="col" className="py-2 pr-2 text-right">
                    Unit price
                  </th>
                  <th scope="col" className="py-2 pr-2 text-right">
                    Taxable
                  </th>
                  <th scope="col" className="py-2 pr-2 text-right">
                    GST
                  </th>
                  <th scope="col" className="py-2 pr-2 text-right">
                    CGST
                  </th>
                  <th scope="col" className="py-2 pr-2 text-right">
                    SGST
                  </th>
                  <th scope="col" className="py-2 text-right">
                    Total
                  </th>
                </tr>
              </thead>
              <tbody>
                {invoice.lines.map((line) => (
                  <tr key={line.lineNo} className="border-border border-b">
                    <td className="py-2 pr-2">{line.lineNo}</td>
                    <td className="py-2 pr-2">{line.description}</td>
                    <td className="py-2 pr-2">{line.hsnCode}</td>
                    <td className="py-2 pr-2 text-right">{formatQuantity(line.quantity)}</td>
                    <td className="py-2 pr-2 text-right">{formatRupees(line.unitPricePaise)}</td>
                    <td className="py-2 pr-2 text-right">{formatRupees(line.taxablePaise)}</td>
                    <td className="py-2 pr-2 text-right">{formatPercentFromBp(line.taxRateBp)}</td>
                    <td className="py-2 pr-2 text-right">{formatRupees(line.cgstPaise)}</td>
                    <td className="py-2 pr-2 text-right">{formatRupees(line.sgstPaise)}</td>
                    <td className="py-2 text-right">{formatRupees(line.totalPaise)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <dl className="mt-4 ml-auto grid max-w-xs grid-cols-2 gap-1 text-sm">
            <dt>Taxable value</dt>
            <dd className="text-right">{formatRupees(invoice.taxablePaise)}</dd>
            <dt>GST</dt>
            <dd className="text-right">{formatRupees(invoice.taxPaise)}</dd>
            <dt className="font-semibold">Invoice total</dt>
            <dd className="text-right font-semibold">{formatRupees(invoice.totalPaise)}</dd>
          </dl>
        </Section>
      </div>
    </>
  );
}

/** The server's PDF, fetched with the staff token (a plain link cannot carry it) and opened from an object URL. */
function PdfLink({ path, name }: { path: string; name: string }) {
  const file = useApiFile(path);
  const href = useObjectUrl(file.data);
  if (!href) {
    return (
      <Button variant="outline" disabled loading={file.isPending}>
        {file.isError ? "PDF unavailable" : "Download PDF"}
      </Button>
    );
  }
  return (
    <a
      href={href}
      download={`${name}.pdf`}
      className="border-border hover:bg-muted inline-flex h-10 items-center gap-2 rounded-full border px-4 text-sm font-medium"
    >
      <FileDown aria-hidden="true" className="size-4" />
      Download PDF
    </a>
  );
}
