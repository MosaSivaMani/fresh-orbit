// Order tracking (docs/api §4.8): status header, delivery code while OUT_FOR_DELIVERY, timeline, items with
// weighed and charged quantities, payment summary, and actions exactly as the server's `allowedActions` allow.
// Refreshes every 30 seconds while the order is moving, and on pull-to-refresh.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/api_client.dart';
import '../../api/api_error.dart';
import '../../api/models/orders.dart';
import '../../core/format.dart';
import '../../core/launch.dart';
import '../../state/core_providers.dart';
import '../../state/orders_state.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';

const _terminal = {'DELIVERED', 'CANCELLED', 'PAYMENT_FAILED', 'RETURNED'};

const _cancelReasons = <String, String>{
  'CHANGED_MIND': 'I changed my mind',
  'WRONG_ADDRESS': 'Wrong address',
  'SLOT_ISSUE': 'The delivery slot does not suit me',
  'OTHER': 'Other reason',
};

const _complaintKinds = <String, String>{
  'MISSING': 'Item missing',
  'DAMAGED': 'Item damaged',
  'QUALITY': 'Quality not as expected',
  'WRONG_ITEM': 'Wrong item',
  'LATE': 'Delivered late',
};

class OrderTrackingScreen extends ConsumerStatefulWidget {
  const OrderTrackingScreen({super.key, required this.orderId});

  final String orderId;

  @override
  ConsumerState<OrderTrackingScreen> createState() => _OrderTrackingScreenState();
}

class _OrderTrackingScreenState extends ConsumerState<OrderTrackingScreen> {
  Timer? _poll;

  @override
  void initState() {
    super.initState();
    _poll = Timer.periodic(const Duration(seconds: 30), (_) {
      final status = ref.read(orderTrackingProvider(widget.orderId)).value?.status;
      if (status != null && !_terminal.contains(status)) _refresh();
    });
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  void _refresh() {
    ref.invalidate(orderDetailProvider(widget.orderId));
    ref.invalidate(orderTrackingProvider(widget.orderId));
  }

  @override
  Widget build(BuildContext context) {
    final detail = ref.watch(orderDetailProvider(widget.orderId));
    final tracking = ref.watch(orderTrackingProvider(widget.orderId));
    return Scaffold(
      appBar: AppBar(title: Text(detail.value?.orderNumber ?? 'Order')),
      body: RefreshIndicator(
        onRefresh: () async {
          _refresh();
          await ref.read(orderDetailProvider(widget.orderId).future);
        },
        child: AsyncValueView<OrderDetail>(
          value: detail,
          onRetry: _refresh,
          data: (order) => _TrackingBody(order: order, tracking: tracking.value, trackingError: tracking.error),
        ),
      ),
    );
  }
}

class _TrackingBody extends ConsumerWidget {
  const _TrackingBody({required this.order, required this.tracking, this.trackingError});

  final OrderDetail order;
  final OrderTracking? tracking;
  final Object? trackingError;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final t = tracking;
    final status = t?.status ?? order.status;
    final amounts = order.finalAmounts ?? order.estimate;
    return ListView(
      padding: const EdgeInsets.all(FreshSpacing.s4),
      children: [
        Row(
          children: [
            Expanded(
              child: Semantics(header: true, child: Text(order.orderNumber, style: theme.textTheme.headlineSmall)),
            ),
            OrderStatusBadge(status: status),
          ],
        ),
        Text('Placed ${formatDateTime(order.placedAt)}', style: theme.textTheme.bodyMedium),
        if ((t?.slot ?? order.slot) != null)
          Text(
            'Delivery ${formatSlotWindow((t?.slot ?? order.slot)!.startAt, (t?.slot ?? order.slot)!.endAt)}',
            style: theme.textTheme.bodyMedium,
          ),
        if (t?.deliveryCode != null) ...[
          const SizedBox(height: FreshSpacing.s4),
          _DeliveryCodeCard(code: t!.deliveryCode!),
        ],
        if (t?.shipment?.riderName != null) ...[
          const SizedBox(height: FreshSpacing.s3),
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: const Icon(Icons.delivery_dining_outlined),
            title: Text('${t!.shipment!.riderName} is delivering your order'),
            trailing: t.shipment!.riderPhone == null
                ? null
                : IconButton(
                    tooltip: 'Call ${t.shipment!.riderName}',
                    icon: const Icon(Icons.call_outlined),
                    onPressed: () => openExternal(context, telUri(t.shipment!.riderPhone!)),
                  ),
          ),
        ],
        const SizedBox(height: FreshSpacing.s5),
        Semantics(header: true, child: Text('Status', style: theme.textTheme.titleMedium)),
        const SizedBox(height: FreshSpacing.s2),
        if (t != null)
          _Timeline(entries: t.timeline)
        else if (trackingError != null)
          const InfoBanner(
            tone: BannerTone.warning,
            text: 'Live status is not available right now. Pull down to refresh.',
          )
        else
          const LoadingSemantics(child: SkeletonBox(height: 80)),
        const SizedBox(height: FreshSpacing.s5),
        Semantics(header: true, child: Text('Items', style: theme.textTheme.titleMedium)),
        for (final item in order.items) _ItemTile(item: item),
        const SizedBox(height: FreshSpacing.s4),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(FreshSpacing.s4),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Semantics(header: true, child: Text('Payment', style: theme.textTheme.titleMedium)),
                const SizedBox(height: FreshSpacing.s2),
                Text(paymentMethodLabel(order.paymentMethod)),
                if (order.payment != null) Text('Status: ${humanizeCode(order.payment!.status)}'),
                const Divider(height: FreshSpacing.s5),
                Text(
                  order.finalAmounts == null
                      ? 'Estimated total ${formatRupees(amounts.totalPaise)}'
                      : 'Final total ${formatRupees(amounts.totalPaise)}',
                  style: theme.textTheme.titleSmall,
                ),
                if (order.finalAmounts == null)
                  Text(
                    'The final total is set when your order is weighed and packed.',
                    style: theme.textTheme.bodySmall,
                  ),
                if (order.amountDuePaise > 0) Text('To pay on delivery: ${formatRupees(order.amountDuePaise)}'),
                if (order.amountPaidPaise > 0) Text('Paid: ${formatRupees(order.amountPaidPaise)}'),
                if (order.amountRefundedPaise > 0) Text('Refunded: ${formatRupees(order.amountRefundedPaise)}'),
                if (order.couponCode != null) Text('Coupon: ${order.couponCode}'),
              ],
            ),
          ),
        ),
        const SizedBox(height: FreshSpacing.s4),
        _Actions(order: order),
        if (order.status == 'DELIVERED') _Complaints(orderId: order.id),
      ],
    );
  }
}

class _DeliveryCodeCard extends StatelessWidget {
  const _DeliveryCodeCard({required this.code});

  final String code;

  @override
  Widget build(BuildContext context) {
    final c = freshColors(context);
    return Card(
      color: c.accentSubtle,
      child: Padding(
        padding: const EdgeInsets.all(FreshSpacing.s4),
        child: Row(
          children: [
            Expanded(
              child: Semantics(
                label: 'Delivery code ${code.split('').join(' ')}. Share it with the delivery partner.',
                excludeSemantics: true,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Delivery code', style: Theme.of(context).textTheme.titleSmall?.copyWith(color: c.accentText)),
                    Text(
                      code,
                      key: const Key('deliveryCode'),
                      style: TextStyle(
                        fontFamily: FreshFonts.mono,
                        fontSize: FreshTypeScale.xl4.size,
                        fontWeight: FontWeight.w700,
                        letterSpacing: 8,
                        color: c.accentText,
                      ),
                    ),
                    Text('Share it only when you receive your order.', style: Theme.of(context).textTheme.bodySmall),
                  ],
                ),
              ),
            ),
            IconButton(
              tooltip: 'Copy delivery code',
              icon: const Icon(Icons.copy),
              onPressed: () async {
                await Clipboard.setData(ClipboardData(text: code));
                if (context.mounted) showInfoSnack(context, 'Delivery code copied.');
              },
            ),
          ],
        ),
      ),
    );
  }
}

class _Timeline extends StatelessWidget {
  const _Timeline({required this.entries});

  final List<TimelineEntry> entries;

  @override
  Widget build(BuildContext context) {
    final c = freshColors(context);
    return Column(
      children: [
        for (var i = 0; i < entries.length; i++)
          MergeSemantics(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Column(
                  children: [
                    Icon(
                      i == entries.length - 1 ? Icons.radio_button_checked : Icons.check_circle,
                      color: i == entries.length - 1 ? c.accent : c.success,
                      size: 20,
                    ),
                    if (i < entries.length - 1) Container(width: 2, height: 28, color: c.border),
                  ],
                ),
                const SizedBox(width: FreshSpacing.s3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        orderStatusLabel(entries[i].status),
                        style: i == entries.length - 1
                            ? Theme.of(context).textTheme.titleSmall
                            : Theme.of(context).textTheme.bodyMedium,
                      ),
                      Text(formatDateTime(entries[i].at), style: Theme.of(context).textTheme.bodySmall),
                    ],
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}

class _ItemTile extends StatelessWidget {
  const _ItemTile({required this.item});

  final OrderItem item;

  @override
  Widget build(BuildContext context) {
    final subtitle = <String>[
      [item.variantName, item.preparationName].whereType<String>().join(' · '),
      'Ordered ${formatQuantity(item.orderedQuantity)}',
      if (item.packedQuantity != null) 'Weighed ${formatQuantity(item.packedQuantity!)}',
      if (item.chargedQuantity != null) 'Charged for ${formatQuantity(item.chargedQuantity!)}',
      if (item.status != 'ACTIVE') humanizeCode(item.status),
    ].where((s) => s.isNotEmpty).join('\n');
    return ListTile(
      contentPadding: EdgeInsets.zero,
      title: Text(item.productName),
      subtitle: Text(subtitle),
      isThreeLine: true,
      trailing: Text(
        formatRupees(item.finalTotalPaise ?? item.estimatedTotalPaise),
        semanticsLabel: item.finalTotalPaise == null
            ? 'Estimated ${formatRupees(item.estimatedTotalPaise)}'
            : 'Final ${formatRupees(item.finalTotalPaise!)}',
      ),
    );
  }
}

class _Actions extends ConsumerWidget {
  const _Actions({required this.order});

  final OrderDetail order;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final a = order.allowedActions;
    return Wrap(
      spacing: FreshSpacing.s2,
      runSpacing: FreshSpacing.s2,
      children: [
        if (a.cancel)
          OutlinedButton.icon(
            key: const Key('cancelOrder'),
            icon: const Icon(Icons.cancel_outlined),
            label: const Text('Cancel order'),
            onPressed: () => showModalBottomSheet<void>(
              context: context,
              isScrollControlled: true,
              showDragHandle: true,
              builder: (_) => _CancelSheet(orderId: order.id),
            ),
          ),
        if (a.raiseComplaint)
          OutlinedButton.icon(
            icon: const Icon(Icons.report_problem_outlined),
            label: const Text('Report a problem'),
            onPressed: () => showModalBottomSheet<void>(
              context: context,
              isScrollControlled: true,
              showDragHandle: true,
              builder: (_) => _ComplaintSheet(order: order),
            ),
          ),
        if (a.reorder)
          OutlinedButton.icon(
            icon: const Icon(Icons.replay),
            label: const Text('Order again'),
            onPressed: () async {
              try {
                await ref.read(orderActionsProvider).reorder(order.id);
                if (context.mounted) context.go('/cart');
              } catch (e) {
                if (context.mounted) showErrorSnack(context, e);
              }
            },
          ),
        if (a.downloadInvoice)
          OutlinedButton.icon(
            icon: const Icon(Icons.receipt_outlined),
            label: const Text('Invoice'),
            onPressed: () async {
              try {
                final link = await ref.read(orderRepositoryProvider).invoice(order.id);
                if (!context.mounted) return;
                if (link.pdfUrl == null) {
                  showInfoSnack(context, 'The invoice is not ready yet.');
                } else {
                  await openExternal(context, Uri.parse(link.pdfUrl!));
                }
              } catch (e) {
                if (context.mounted) showErrorSnack(context, e);
              }
            },
          ),
      ],
    );
  }
}

/// One Idempotency-Key per sheet (one user action), reused when the outcome is unknown, renewed after a
/// definitive refusal.
mixin _IdempotentSubmit<T extends StatefulWidget> on State<T> {
  String _key = newIdempotencyKey();
  bool busy = false;
  String? error;

  Future<bool> submit(Future<void> Function(String key) action) async {
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await action(_key);
      return true;
    } on ApiError catch (e) {
      if (!e.isTransient) _key = newIdempotencyKey();
      setState(() => error = e.message);
      return false;
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }
}

class _CancelSheet extends ConsumerStatefulWidget {
  const _CancelSheet({required this.orderId});

  final String orderId;

  @override
  ConsumerState<_CancelSheet> createState() => _CancelSheetState();
}

class _CancelSheetState extends ConsumerState<_CancelSheet> with _IdempotentSubmit {
  String _reason = 'CHANGED_MIND';

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(FreshSpacing.s4, 0, FreshSpacing.s4, FreshSpacing.s4),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Semantics(header: true, child: Text('Cancel this order?', style: Theme.of(context).textTheme.titleLarge)),
            RadioGroup<String>(
              groupValue: _reason,
              onChanged: (v) => setState(() => _reason = v!),
              child: Column(
                children: [
                  for (final e in _cancelReasons.entries)
                    RadioListTile<String>(contentPadding: EdgeInsets.zero, value: e.key, title: Text(e.value)),
                ],
              ),
            ),
            if (error != null) InfoBanner(tone: BannerTone.danger, text: error!),
            const SizedBox(height: FreshSpacing.s3),
            PrimaryButton(
              label: 'Cancel order',
              loading: busy,
              onPressed: () async {
                final ok = await submit(
                  (key) => ref.read(orderActionsProvider).cancel(widget.orderId, _reason, idempotencyKey: key),
                );
                if (ok && context.mounted) {
                  Navigator.pop(context);
                  showInfoSnack(context, 'Your order is cancelled.');
                }
              },
            ),
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('Keep my order')),
          ],
        ),
      ),
    );
  }
}

class _ComplaintSheet extends ConsumerStatefulWidget {
  const _ComplaintSheet({required this.order});

  final OrderDetail order;

  @override
  ConsumerState<_ComplaintSheet> createState() => _ComplaintSheetState();
}

class _ComplaintSheetState extends ConsumerState<_ComplaintSheet> with _IdempotentSubmit {
  String _kind = 'QUALITY';
  String? _itemId;
  final _description = TextEditingController();
  String? _fieldError;

  @override
  void dispose() {
    _description.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final needsItem = _kind != 'LATE';
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          FreshSpacing.s4,
          0,
          FreshSpacing.s4,
          FreshSpacing.s4 + MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Semantics(header: true, child: Text('Report a problem', style: Theme.of(context).textTheme.titleLarge)),
              const SizedBox(height: FreshSpacing.s2),
              Wrap(
                spacing: FreshSpacing.s2,
                runSpacing: FreshSpacing.s2,
                children: [
                  for (final e in _complaintKinds.entries)
                    ChoiceChip(
                      label: Text(e.value),
                      selected: _kind == e.key,
                      onSelected: (_) => setState(() => _kind = e.key),
                    ),
                ],
              ),
              if (needsItem) ...[
                const SizedBox(height: FreshSpacing.s3),
                DropdownButtonFormField<String>(
                  initialValue: _itemId,
                  decoration: const InputDecoration(labelText: 'Which item?'),
                  items: [
                    for (final i in widget.order.items) DropdownMenuItem(value: i.id, child: Text(i.productName)),
                  ],
                  onChanged: (v) => setState(() => _itemId = v),
                ),
              ],
              const SizedBox(height: FreshSpacing.s3),
              TextField(
                controller: _description,
                minLines: 3,
                maxLines: 6,
                maxLength: 1000,
                decoration: InputDecoration(labelText: 'What went wrong?', errorText: _fieldError),
              ),
              Text(
                'Photo upload will be added soon; our team may call you for photos.',
                style: Theme.of(context).textTheme.bodySmall,
              ),
              if (error != null) InfoBanner(tone: BannerTone.danger, text: error!),
              const SizedBox(height: FreshSpacing.s3),
              PrimaryButton(
                label: 'Send',
                loading: busy,
                onPressed: () async {
                  if (needsItem && _itemId == null) {
                    setState(() => _fieldError = 'Choose the item with the problem.');
                    return;
                  }
                  if (_description.text.trim().isEmpty) {
                    setState(() => _fieldError = 'Describe the problem.');
                    return;
                  }
                  setState(() => _fieldError = null);
                  final ok = await submit(
                    (key) => ref
                        .read(orderActionsProvider)
                        .raiseComplaint(
                          widget.order.id,
                          orderItemId: needsItem ? _itemId : null,
                          kind: _kind,
                          description: _description.text.trim(),
                          idempotencyKey: key,
                        ),
                  );
                  if (ok && context.mounted) {
                    Navigator.pop(context);
                    showInfoSnack(context, 'Thank you. Our team will get back to you.');
                  }
                },
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Complaints extends ConsumerWidget {
  const _Complaints({required this.orderId});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final list = ref.watch(orderComplaintsProvider(orderId)).value ?? const <Complaint>[];
    if (list.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(top: FreshSpacing.s4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Semantics(header: true, child: Text('Reported problems', style: Theme.of(context).textTheme.titleMedium)),
          for (final c in list)
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text(_complaintKinds[c.kind] ?? humanizeCode(c.kind)),
              subtitle: c.description == null ? null : Text(c.description!),
              trailing: FreshBadge(label: humanizeCode(c.status)),
            ),
        ],
      ),
    );
  }
}
