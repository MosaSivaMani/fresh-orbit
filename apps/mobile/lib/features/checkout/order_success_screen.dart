// Order placed (D288, owner's reference docs/design/mockups/app-order-placed-reference-2026-10-29.png): the teal
// header with Padma (ORDER_PLACED) at the right, the server's order on a cream ticket, the delivery-code note
// with the rider, Track order / Continue shopping, and the sea floor at the bottom (D289). The screen only
// composes `fresh_theme` parts (D207); every value is the server's order, only formatted (rule 4).

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/orders.dart';
import '../../core/format.dart';
import '../../illustrations/illustration.dart';
import '../../state/checkout_state.dart';
import '../../state/orders_state.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';

class OrderSuccessScreen extends ConsumerWidget {
  const OrderSuccessScreen({super.key, required this.orderId});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final live = ref.watch(orderDetailProvider(orderId));
    final placed = ref.watch(placedOrdersProvider)[orderId];
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) context.go('/home');
      },
      child: Scaffold(
        backgroundColor: c.sand,
        body: AsyncValueView<OrderDetail>(
          // The order `POST /orders` returned is shown at once, and stays when the fresh read fails (MO-4): the
          // customer always sees "Order placed" with the number and Track order.
          value: placed != null && !live.hasValue ? AsyncData(placed) : live,
          onRetry: () => ref.invalidate(orderDetailProvider(orderId)),
          loading: const SafeArea(child: ListSkeleton()),
          data: (order) => _OrderPlaced(order: order),
        ),
      ),
    );
  }
}

class _OrderPlaced extends StatelessWidget {
  const _OrderPlaced({required this.order});

  final OrderDetail order;

  @override
  Widget build(BuildContext context) {
    final slot = order.slot;
    final address = order.address;
    final total = formatRupees(order.estimate.totalPaise);
    return FreshSeaFloorScroll(
      key: const Key('orderPlacedPage'),
      gap: 20,
      children: [
        FreshCelebrationHeader(
          title: 'Order',
          scriptWord: 'placed',
          status: orderStatusLabel(order.status),
          message: 'Thank you! Your fresh seafood is on its way.',
          character: (size) => Illustration(slot: IllustrationSlot.orderPlaced, size: size),
          child: FreshTicketCard(
            title: 'Order ${order.orderNumber}',
            rows: [
              if (slot != null)
                FreshTicketRow(
                  icon: Icons.calendar_month_outlined,
                  label: 'Delivery',
                  value: '${formatDayShort(slot.startAt)}\n${formatTimeRange(slot.startAt, slot.endAt)}',
                  semanticsLabel: 'Delivery: ${formatSlotWindow(slot.startAt, slot.endAt)}',
                ),
              FreshTicketRow(
                icon: Icons.account_balance_wallet_outlined,
                label: 'Payment',
                value: paymentMethodLabel(order.paymentMethod),
                tone: FreshTicketTone.coral,
                strong: 'delivery',
              ),
              if (address != null)
                FreshTicketRow(icon: Icons.location_on_outlined, label: 'Deliver to', value: address.oneLine),
            ],
            totalLabel: 'Estimated total',
            totalValue: total,
            totalSemanticsLabel: 'Estimated total $total',
            note: 'The final total is set when your order is weighed and packed.',
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(
            FreshCelebrationHeader.gutter,
            16,
            FreshCelebrationHeader.gutter,
            0,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const FreshNoteCard(
                key: Key('deliveryCodeNote'),
                text:
                    'When your order is out for delivery, a 4-digit delivery code appears in Orders. '
                    'Share it with the delivery partner when you receive your order.',
                picture: Illustration(slot: IllustrationSlot.homeCardRider, size: 88, excludeFromSemantics: true),
              ),
              const SizedBox(height: 18),
              FreshArrowButton(
                key: const Key('trackOrder'),
                label: 'Track order',
                onPressed: () => context.go('/orders/${order.id}'),
              ),
              const SizedBox(height: 12),
              FreshOutlineArrowButton(
                key: const Key('continueShopping'),
                label: 'Continue shopping',
                icon: Icons.shopping_basket_outlined,
                onPressed: () => context.go('/home'),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
