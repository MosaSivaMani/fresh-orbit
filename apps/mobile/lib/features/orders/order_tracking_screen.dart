// Order tracking (docs/api §4.8, D88): a teal block with the status and its picture in the ringed circle, a
// paper sheet with the slot, four progress bars, delivery code while OUT_FOR_DELIVERY, timeline, items with
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
import '../../core/photo_picker.dart';
import '../../illustrations/illustration_slot.dart';
import '../../state/core_providers.dart';
import '../../state/orders_state.dart';
import '../../widgets/bold.dart';
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
    final c = illustratedColors(context);
    final loaded = detail.value != null;
    return Scaffold(
      backgroundColor: loaded ? BlockTone.teal.background(c) : null,
      appBar: loaded ? null : const PageHeader(text: 'Your', brushed: 'order'),
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
    final c = illustratedColors(context);
    final t = tracking;
    final status = t?.status ?? order.status;
    final slot = t?.slot ?? order.slot;
    final sheet = <Widget>[
      Row(
        children: [
          Expanded(
            child: Text(
              'Placed ${formatDateTime(order.placedAt)}',
              style: theme.textTheme.bodySmall?.copyWith(color: c.muted),
            ),
          ),
          OrderStatusBadge(status: status),
        ],
      ),
      if (slot != null) ...[
        const SizedBox(height: FreshSpacing.s2),
        Text('ARRIVING', style: AppText.eyebrow(c.muted, size: 10.5)),
        Text(formatSlotWindow(slot.startAt, slot.endAt), style: AppText.display(c.ink, 19)),
      ],
      if (_progress(status) case final reached?) ...[
        const SizedBox(height: FreshSpacing.s3),
        ProgressSteps(labels: const ['Placed', 'Packed', 'On the way', 'Delivered'], reached: reached),
      ],
      if (order.substitutions.isNotEmpty) _Substitutions(order: order),
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
      const PageTitle(text: 'Order', brushed: 'status', size: 19),
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
      _OrderReceipt(order: order),
      const SizedBox(height: FreshSpacing.s4),
      _Actions(order: order),
      if (order.status == 'DELIVERED') _Complaints(orderId: order.id),
    ];
    return ListView(
      padding: EdgeInsets.zero,
      children: [
        _StatusHero(
          status: status,
          orderNumber: order.orderNumber,
          deliveryCode: t?.deliveryCode,
          onBack: context.canPop() ? () => context.pop() : () => context.go('/orders'),
        ),
        Container(
          constraints: BoxConstraints(minHeight: MediaQuery.sizeOf(context).height * 0.5),
          decoration: BoxDecoration(
            color: c.paper,
            borderRadius: const BorderRadius.vertical(top: Radius.circular(AppRadius.sheet)),
          ),
          padding: EdgeInsets.fromLTRB(
            FreshLayout.gutter + 4,
            FreshLayout.sectionGap,
            FreshLayout.gutter + 4,
            FreshLayout.sectionGap + MediaQuery.paddingOf(context).bottom,
          ),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: sheet),
        ),
      ],
    );
  }

  /// How many of the four bars are done; null for closed or failed orders (the badge says so).
  static int? _progress(String status) => switch (status) {
    'PENDING_PAYMENT' || 'CONFIRMED' => 1,
    'PREPARING' || 'PACKED' => 2,
    'OUT_FOR_DELIVERY' => 3,
    'DELIVERED' => 4,
    _ => null,
  };
}

/// The illustration follows the server's status (placed → packing → on the way → delivered, or cancelled). The
/// status text sits next to it, so the picture is decorative for screen readers.
class _StatusHero extends StatelessWidget {
  const _StatusHero({required this.status, required this.orderNumber, required this.onBack, this.deliveryCode});

  final String status;
  final String orderNumber;
  final String? deliveryCode;
  final VoidCallback onBack;

  static String _line(String status) => switch (status) {
    'PREPARING' || 'PACKED' => 'We are cleaning and packing your order.',
    'OUT_FOR_DELIVERY' => 'Your order is on the way, kept cold.',
    'DELIVERED' => 'Delivered. Enjoy your meal!',
    'CANCELLED' || 'PAYMENT_FAILED' || 'RETURNED' => 'This order is closed.',
    'DELIVERY_FAILED' => 'We could not deliver this time.',
    _ => 'We have your order.',
  };

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final fg = BlockTone.teal.foreground(c);
    final slot = slotForOrderStatus(status);
    final image = AvatarCircle(key: ValueKey(slot), slot: slot, size: 150);
    return SafeArea(
      bottom: false,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            BackCircleButton(onTap: onBack),
            const SizedBox(height: 8),
            Text('ORDER $orderNumber', style: AppText.eyebrow(fg.withValues(alpha: 0.85))),
            const SizedBox(height: 4),
            Semantics(
              liveRegion: true,
              header: true,
              child: Text(orderStatusLabel(status), style: AppText.display(fg, 32, height: 1.0)),
            ),
            Text(
              _line(status),
              style: TextStyle(fontFamily: AppFonts.body, fontSize: 14, color: fg),
            ),
            const SizedBox(height: 14),
            Center(
              child: Stack(
                clipBehavior: Clip.none,
                children: [
                  if (motionAllowed(context)) AnimatedSwitcher(duration: FreshDurations.slow, child: image) else image,
                  if (deliveryCode != null)
                    Positioned(
                      right: -18,
                      top: 18,
                      child: ExcludeSemantics(
                        child: Sticker(label: 'Code $deliveryCode', tone: StickerTone.mango, tilt: 0.017),
                      ),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _DeliveryCodeCard extends StatelessWidget {
  const _DeliveryCodeCard({required this.code});

  final String code;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    return Card(
      color: c.mangoSoft,
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
                    Text('Delivery code', style: AppText.eyebrow(c.ink, size: 11)),
                    Text(
                      code,
                      key: const Key('deliveryCode'),
                      style: TextStyle(
                        fontFamily: AppFonts.display,
                        fontSize: FreshTypeScale.xl4.size,
                        fontWeight: FontWeight.w800,
                        letterSpacing: 8,
                        fontFeatures: const [FontFeature.tabularFigures()],
                        color: c.ink,
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

/// The order as a paper receipt: each item with its ordered, weighed and charged quantities, the fees, payment
/// and the total. Every amount is the server's, only formatted (rule 4).
class _OrderReceipt extends StatelessWidget {
  const _OrderReceipt({required this.order});

  final OrderDetail order;

  @override
  Widget build(BuildContext context) {
    final finalAmounts = order.finalAmounts;
    final amounts = finalAmounts ?? order.estimate;
    final totalLabel = finalAmounts == null ? 'Estimated total' : 'Final total';
    final rows = <ReceiptRow>[
      for (final item in order.items)
        ReceiptRow(
          [item.productName, item.preparationName].whereType<String>().join(', '),
          formatRupees(item.finalTotalPaise ?? item.estimatedTotalPaise),
          detail: [
            'Ordered ${formatQuantity(item.orderedQuantity)}',
            if (item.packedQuantity != null) 'weighed ${formatQuantity(item.packedQuantity!)}',
            if (item.chargedQuantity != null) 'charged for ${formatQuantity(item.chargedQuantity!)}',
            if (item.status != 'ACTIVE') humanizeCode(item.status),
          ].join(', '),
          semanticsLabel:
              '${item.productName}, ${item.finalTotalPaise == null ? 'estimated' : 'final'} '
              '${formatRupees(item.finalTotalPaise ?? item.estimatedTotalPaise)}',
        ),
      ReceiptRow('Delivery', formatRupees(amounts.deliveryFeePaise)),
      if (amounts.discountPaise > 0)
        ReceiptRow(
          order.couponCode == null ? 'Discount' : 'Discount (${order.couponCode})',
          formatRupees(-amounts.discountPaise),
        ),
      ReceiptRow(
        'Payment',
        paymentMethodLabel(order.paymentMethod),
        detail: order.payment == null ? null : humanizeCode(order.payment!.status),
      ),
      if (order.amountDuePaise > 0) ReceiptRow(totalLabel, formatRupees(amounts.totalPaise)),
      if (order.amountPaidPaise > 0) ReceiptRow('Paid', formatRupees(order.amountPaidPaise)),
      if (order.amountRefundedPaise > 0) ReceiptRow('Refunded', formatRupees(order.amountRefundedPaise)),
    ];
    return ReceiptCard(
      title: 'Fresh Orbitt',
      subtitle: 'Order ${order.orderNumber} · placed ${formatDateTime(order.placedAt)}',
      rows: rows,
      total: order.amountDuePaise > 0
          ? ReceiptRow('To pay at the door', formatRupees(order.amountDuePaise))
          : ReceiptRow(totalLabel, formatRupees(amounts.totalPaise)),
      note: finalAmounts == null
          ? 'The final total is set when your order is weighed and packed, never above the maximum shown at '
                'checkout.'
          : null,
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
            onPressed: () async {
              final sent = await showModalBottomSheet<bool>(
                context: context,
                isScrollControlled: true,
                showDragHandle: true,
                builder: (_) => _ComplaintSheet(order: order),
              );
              if (sent == true && context.mounted) await showComplaintReceived(context);
            },
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
  final List<ComplaintPhotoUpload> _photos = [];

  /// Photos per complaint the app offers (the server checks its own limit).
  static const _maxPhotos = 4;

  Future<void> _addPhoto() async {
    try {
      final photo = await ref.read(photoPickerProvider)();
      if (photo != null && mounted) setState(() => _photos.add(photo));
    } catch (e) {
      if (mounted) showErrorSnack(context, e);
    }
  }

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
              const PageTitle(text: 'Report a', brushed: 'problem', size: 22),
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
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Which item?'),
                  items: [
                    for (final i in widget.order.items)
                      DropdownMenuItem(
                        value: i.id,
                        child: Text(i.productName, maxLines: 1, overflow: TextOverflow.ellipsis),
                      ),
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
              Wrap(
                key: const Key('complaintPhotos'),
                spacing: 8,
                runSpacing: 8,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  for (var i = 0; i < _photos.length; i++)
                    Stack(
                      clipBehavior: Clip.none,
                      children: [
                        ClipRRect(
                          borderRadius: BorderRadius.circular(FreshLayout.tileRadius),
                          child: Image.memory(
                            _photos[i].bytes,
                            width: 72,
                            height: 48,
                            fit: BoxFit.cover,
                            semanticLabel: 'Photo ${i + 1}',
                            errorBuilder: (_, _, _) => const SizedBox(width: 72, height: 48),
                          ),
                        ),
                        Positioned(
                          right: -10,
                          top: -10,
                          child: IconButton(
                            tooltip: 'Remove photo ${i + 1}',
                            visualDensity: VisualDensity.compact,
                            icon: const Icon(Icons.cancel_rounded, size: 20),
                            onPressed: busy ? null : () => setState(() => _photos.removeAt(i)),
                          ),
                        ),
                      ],
                    ),
                  if (_photos.length < _maxPhotos)
                    OutlinedButton.icon(
                      key: const Key('addComplaintPhoto'),
                      onPressed: busy ? null : _addPhoto,
                      icon: const Icon(Icons.add_a_photo_outlined),
                      label: Text(_photos.isEmpty ? 'Add photos' : 'Add another'),
                    ),
                ],
              ),
              const SizedBox(height: 8),
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
                          photos: List.of(_photos),
                        ),
                  );
                  if (ok && context.mounted) Navigator.pop(context, true);
                },
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// "Complaint received" with the COMPLAINT_RECEIVED character, after the server accepted the complaint.
Future<void> showComplaintReceived(BuildContext context) => showDialog<void>(
  context: context,
  builder: (context) => AlertDialog(
    icon: const AvatarCircle(slot: IllustrationSlot.complaintReceived, size: 132),
    title: const Text('We have your report', textAlign: TextAlign.center),
    content: const Text(
      'Thank you for telling us. Our team will look into it and get back to you.',
      textAlign: TextAlign.center,
    ),
    actionsAlignment: MainAxisAlignment.center,
    actions: [FilledButton(onPressed: () => Navigator.pop(context), child: const Text('Done'))],
  ),
);

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

/// Replacements the store proposed for short items. A PROPOSED one shows Accept and Reject; the server answers
/// with the updated order. The price difference is the server's, only formatted.
class _Substitutions extends ConsumerStatefulWidget {
  const _Substitutions({required this.order});

  final OrderDetail order;

  @override
  ConsumerState<_Substitutions> createState() => _SubstitutionsState();
}

class _SubstitutionsState extends ConsumerState<_Substitutions> {
  String? _busy;

  Future<void> _answer(OrderSubstitution s, {required bool accept}) async {
    setState(() => _busy = s.id);
    try {
      await ref.read(orderActionsProvider).answerSubstitution(widget.order.id, s.id, accept: accept);
      if (mounted) showInfoSnack(context, accept ? 'Replacement accepted.' : 'Replacement declined.');
    } catch (e) {
      if (mounted) showErrorSnack(context, e);
    } finally {
      if (mounted) setState(() => _busy = null);
    }
  }

  static String _difference(int paise) => paise == 0
      ? 'Same price'
      : paise > 0
      ? '${formatRupees(paise)} more'
      : '${formatRupees(-paise)} less';

  static String _status(String status) => switch (status) {
    'ACCEPTED' => 'Accepted',
    'REJECTED' => 'Declined',
    'EXPIRED' => 'Expired',
    _ => humanizeCode(status),
  };

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final theme = Theme.of(context);
    final order = widget.order;
    return Padding(
      padding: const EdgeInsets.only(top: FreshLayout.sectionGap),
      child: Column(
        key: const Key('orderSubstitutions'),
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const PageTitle(text: 'A replacement', brushed: 'for you', size: 19),
          const SizedBox(height: 8),
          for (final s in order.substitutions) ...[
            SectionBox(
              key: ValueKey('substitution-${s.id}'),
              color: s.awaitsAnswer ? c.mangoSoft : null,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Instead of ${order.items.where((i) => i.id == s.orderItemId).firstOrNull?.productName ?? 'an item'}',
                    style: theme.textTheme.bodySmall?.copyWith(color: c.muted),
                  ),
                  Text(
                    '${s.proposedName}, ${formatQuantity(s.proposedQuantity)}',
                    style: AppText.display(c.ink, 16),
                  ),
                  Text(_difference(s.priceDifferencePaise), style: theme.textTheme.bodyMedium),
                  if (s.awaitsAnswer) ...[
                    Text('Answer by ${formatTime(s.expiresAt)}', style: theme.textTheme.bodySmall),
                    const SizedBox(height: 8),
                    if (_busy == s.id)
                      const LinearProgressIndicator()
                    else
                      Row(
                        children: [
                          Expanded(
                            child: OutlinedButton(
                              key: ValueKey('rejectSubstitution-${s.id}'),
                              onPressed: _busy != null ? null : () => _answer(s, accept: false),
                              child: const Text('No, thanks'),
                            ),
                          ),
                          const SizedBox(width: FreshLayout.cardGap),
                          Expanded(
                            child: FilledButton(
                              key: ValueKey('acceptSubstitution-${s.id}'),
                              onPressed: _busy != null ? null : () => _answer(s, accept: true),
                              child: const Text('Accept'),
                            ),
                          ),
                        ],
                      ),
                  ] else ...[
                    const SizedBox(height: 6),
                    FreshBadge(label: _status(s.status)),
                  ],
                ],
              ),
            ),
            const SizedBox(height: FreshLayout.cardGap),
          ],
        ],
      ),
    );
  }
}
