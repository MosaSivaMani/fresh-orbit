// Orders (D88): signed out and empty are mango colour blocks; the list is paper cards with the server's status,
// slot and total.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/orders.dart';
import '../../core/format.dart';
import '../../illustrations/illustration_slot.dart';
import '../../state/orders_state.dart';
import '../../state/session.dart';
import '../../theme/app_theme.dart';
import '../../widgets/bold.dart';
import '../../widgets/commerce.dart';
import '../../widgets/heading.dart';
import '../../widgets/illustrated.dart';
import '../../widgets/states.dart';

class OrdersScreen extends ConsumerWidget {
  const OrdersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = illustratedColors(context);
    final signedIn = ref.watch(sessionProvider.select((s) => s.signedIn));
    if (!signedIn) {
      return Scaffold(
        backgroundColor: BlockTone.mango.background(c),
        body: ColorBlockScreen(
          tone: BlockTone.mango,
          eyebrow: 'Your orders',
          heading: const [HeadingLine('Sign in to'), HeadingLine('see them', script: true, brush: true)],
          slot: IllustrationSlot.loginOtp,
          avatarSize: AvatarSize.small,
          children: [
            const SheetTitle('Sign in to see your orders', header: true),
            const SheetNote('Your orders and their delivery status will appear here.'),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: () => context.push(Uri(path: '/login', queryParameters: {'from': '/orders'}).toString()),
                child: const Text('Sign in'),
              ),
            ),
          ],
        ),
      );
    }
    final orders = ref.watch(ordersProvider);
    if (orders.value?.items.isEmpty ?? false) {
      return Scaffold(
        backgroundColor: BlockTone.mango.background(c),
        body: ColorBlockScreen(
          tone: BlockTone.mango,
          eyebrow: 'Your orders',
          heading: const [HeadingLine('No orders'), HeadingLine('yet', script: true, brush: true)],
          slot: IllustrationSlot.emptyOrders,
          sticker: const StickerSpot(
            left: 22,
            top: 40,
            child: Sticker(label: 'Pot’s waiting', tone: StickerTone.coral, tilt: -0.035),
          ),
          children: [
            const SheetTitle('No orders yet', header: true),
            const SheetNote('Your first fish curry is one tap away.'),
            SizedBox(
              width: double.infinity,
              child: FilledButton(onPressed: () => context.go('/shop'), child: const Text('Start shopping')),
            ),
          ],
        ),
      );
    }
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: () => ref.refresh(ordersProvider.future),
          child: AsyncValueView<OrdersState>(
            value: orders,
            onRetry: () => ref.invalidate(ordersProvider),
            data: (orders) => NotificationListener<ScrollNotification>(
              onNotification: (n) {
                if (n.metrics.extentAfter < 300 && orders.hasMore && !orders.loadingMore) {
                  ref.read(ordersProvider.notifier).loadMore().catchError((Object e) {
                    if (context.mounted) showErrorSnack(context, e);
                  });
                }
                return false;
              },
              child: ListView.separated(
                padding: const EdgeInsets.fromLTRB(12, 4, 12, 16),
                itemCount: orders.items.length + (orders.loadingMore ? 1 : 0) + 1,
                separatorBuilder: (_, _) => const SizedBox(height: 10),
                itemBuilder: (context, i) {
                  if (i == 0) {
                    return const Padding(
                      padding: EdgeInsets.fromLTRB(4, 0, 4, 0),
                      child: ScriptTitle(text: 'My', script: 'orders', size: 21),
                    );
                  }
                  final index = i - 1;
                  if (index == orders.items.length) {
                    return const Center(child: CircularProgressIndicator(semanticsLabel: 'Loading more'));
                  }
                  return _OrderCard(order: orders.items[index]);
                },
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _OrderCard extends StatelessWidget {
  const _OrderCard({required this.order});

  final OrderSummary order;

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final muted = Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted);
    final o = order;
    return Material(
      color: c.paper,
      borderRadius: BorderRadius.circular(18),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.go('/orders/${o.id}'),
        child: Container(
          decoration: BoxDecoration(boxShadow: cardShadow(context)),
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              AvatarCircle(slot: slotForOrderStatus(o.status), size: 52),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(child: Text(o.orderNumber, style: AppText.display(c.ink, 15))),
                        OrderStatusBadge(status: o.status),
                      ],
                    ),
                    Text('Placed ${formatDateTime(o.placedAt)}', style: muted),
                    if (o.slot != null)
                      Text('Delivery ${formatSlotWindow(o.slot!.startAt, o.slot!.endAt)}', style: muted),
                    if (o.totalPaise != null) Text(formatRupees(o.totalPaise!), style: AppText.price(c.ink, size: 15)),
                  ],
                ),
              ),
              Icon(Icons.chevron_right_rounded, color: c.muted),
            ],
          ),
        ),
      ),
    );
  }
}
