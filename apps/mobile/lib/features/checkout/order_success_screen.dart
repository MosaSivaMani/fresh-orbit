// Order placed (D88): a teal block with the ORDER_PLACED character in the ringed circle and the server's order
// as a paper receipt.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/orders.dart';
import '../../core/format.dart';
import '../../state/orders_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/bold.dart';
import '../../widgets/commerce.dart';
import '../../widgets/heading.dart';
import '../../widgets/illustrated.dart';
import '../../widgets/motion.dart';
import '../../widgets/states.dart';

class OrderSuccessScreen extends ConsumerWidget {
  const OrderSuccessScreen({super.key, required this.orderId});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) context.go('/home');
      },
      child: Scaffold(
        backgroundColor: BlockTone.teal.background(c),
        body: SafeArea(
          child: AsyncValueView<OrderDetail>(
            value: ref.watch(orderDetailProvider(orderId)),
            onRetry: () => ref.invalidate(orderDetailProvider(orderId)),
            data: (order) => ListView(
              padding: const EdgeInsets.fromLTRB(FreshSpacing.s5, FreshSpacing.s5, FreshSpacing.s5, FreshSpacing.s6),
              children: [
                const Center(
                  child: EntranceMotion(child: AvatarCircle(slot: IllustrationSlot.orderPlaced, size: 180)),
                ),
                const SizedBox(height: FreshSpacing.s4),
                Semantics(
                  liveRegion: true,
                  child: Heading(
                    lines: const [HeadingLine('Order'), HeadingLine('placed', script: true, brush: true)],
                    color: BlockTone.teal.foreground(c),
                  ),
                ),
                const SizedBox(height: FreshSpacing.s2),
                Align(
                  alignment: Alignment.centerLeft,
                  child: OrderStatusBadge(status: order.status),
                ),
                const SizedBox(height: FreshSpacing.s5),
                // Every value is the server's order, only formatted (rule 4).
                ReceiptCard(
                  title: 'Order ${order.orderNumber}',
                  rows: [
                    if (order.slot != null)
                      ReceiptRow('Delivery', formatSlotWindow(order.slot!.startAt, order.slot!.endAt)),
                    ReceiptRow('Payment', paymentMethodLabel(order.paymentMethod)),
                    if (order.address != null) ReceiptRow('Deliver to', order.address!.oneLine),
                  ],
                  total: ReceiptRow(
                    'Estimated total',
                    formatRupees(order.estimate.totalPaise),
                    semanticsLabel: 'Estimated total ${formatRupees(order.estimate.totalPaise)}',
                  ),
                  note: 'The final total is set when your order is weighed and packed.',
                ),
                const SizedBox(height: FreshSpacing.s3),
                const InfoBanner(
                  text:
                      'When your order is out for delivery, a 4-digit delivery code appears in Orders. '
                      'Share it with the delivery partner when you receive your order.',
                ),
                const SizedBox(height: FreshSpacing.s5),
                FilledButton(onPressed: () => context.go('/orders/${order.id}'), child: const Text('Track order')),
                const SizedBox(height: FreshSpacing.s2),
                OutlinedButton(
                  style: OutlinedButton.styleFrom(
                    foregroundColor: BlockTone.teal.foreground(c),
                    side: BorderSide(color: BlockTone.teal.foreground(c), width: 1.5),
                  ),
                  onPressed: () => context.go('/home'),
                  child: const Text('Continue shopping'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
