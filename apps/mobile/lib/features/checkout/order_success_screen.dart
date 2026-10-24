import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/orders.dart';
import '../../core/format.dart';
import '../../illustrations/illustration.dart';
import '../../state/orders_state.dart';
import '../../widgets/commerce.dart';
import '../../widgets/illustrated.dart';
import '../../widgets/motion.dart';
import '../../widgets/states.dart';

class OrderSuccessScreen extends ConsumerWidget {
  const OrderSuccessScreen({super.key, required this.orderId});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) context.go('/home');
      },
      child: Scaffold(
        body: SafeArea(
          child: AsyncValueView<OrderDetail>(
            value: ref.watch(orderDetailProvider(orderId)),
            onRetry: () => ref.invalidate(orderDetailProvider(orderId)),
            data: (order) => ListView(
              padding: const EdgeInsets.all(FreshSpacing.s6),
              children: [
                const Center(
                  child: EntranceMotion(
                    child: Illustration(slot: IllustrationSlot.orderPlaced, size: 180, excludeFromSemantics: true),
                  ),
                ),
                const SizedBox(height: FreshSpacing.s3),
                Semantics(
                  header: true,
                  liveRegion: true,
                  child: Text('Order placed', style: theme.textTheme.headlineMedium, textAlign: TextAlign.center),
                ),
                const SizedBox(height: FreshSpacing.s1),
                Center(child: OrderStatusBadge(status: order.status)),
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
                PrimaryButton(label: 'Track order', onPressed: () => context.go('/orders/${order.id}')),
                const SizedBox(height: FreshSpacing.s2),
                OutlinedButton(onPressed: () => context.go('/home'), child: const Text('Continue shopping')),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
