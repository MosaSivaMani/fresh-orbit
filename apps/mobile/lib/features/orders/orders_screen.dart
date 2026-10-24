import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../core/format.dart';
import '../../state/orders_state.dart';
import '../../state/session.dart';
import '../../widgets/commerce.dart';
import '../../widgets/states.dart';

class OrdersScreen extends ConsumerWidget {
  const OrdersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final signedIn = ref.watch(sessionProvider.select((s) => s.signedIn));
    return Scaffold(
      appBar: AppBar(title: const Text('My orders')),
      body: !signedIn
          ? EmptyState(
              slot: IllustrationSlot.loginOtp,
              title: 'Sign in to see your orders',
              action: FilledButton(
                onPressed: () => context.push(Uri(path: '/login', queryParameters: {'from': '/orders'}).toString()),
                child: const Text('Sign in'),
              ),
            )
          : RefreshIndicator(
              onRefresh: () => ref.refresh(ordersProvider.future),
              child: AsyncValueView<OrdersState>(
                value: ref.watch(ordersProvider),
                onRetry: () => ref.invalidate(ordersProvider),
                data: (orders) => orders.items.isEmpty
                    ? EmptyState(
                        slot: IllustrationSlot.emptyOrders,
                        title: 'No orders yet',
                        message: 'Your orders and their delivery status will appear here.',
                        action: FilledButton(onPressed: () => context.go('/shop'), child: const Text('Start shopping')),
                      )
                    : NotificationListener<ScrollNotification>(
                        onNotification: (n) {
                          if (n.metrics.extentAfter < 300 && orders.hasMore && !orders.loadingMore) {
                            ref.read(ordersProvider.notifier).loadMore().catchError((Object e) {
                              if (context.mounted) showErrorSnack(context, e);
                            });
                          }
                          return false;
                        },
                        child: ListView.separated(
                          padding: const EdgeInsets.all(FreshSpacing.s4),
                          itemCount: orders.items.length + (orders.loadingMore ? 1 : 0),
                          separatorBuilder: (_, _) => const SizedBox(height: FreshSpacing.s3),
                          itemBuilder: (context, i) {
                            if (i == orders.items.length) {
                              return const Center(child: CircularProgressIndicator(semanticsLabel: 'Loading more'));
                            }
                            final o = orders.items[i];
                            return Card(
                              child: ListTile(
                                contentPadding: const EdgeInsets.all(FreshSpacing.s3),
                                onTap: () => context.go('/orders/${o.id}'),
                                title: Row(
                                  children: [
                                    Expanded(child: Text(o.orderNumber, style: Theme.of(context).textTheme.titleSmall)),
                                    OrderStatusBadge(status: o.status),
                                  ],
                                ),
                                subtitle: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text('Placed ${formatDateTime(o.placedAt)}'),
                                    if (o.slot != null)
                                      Text('Delivery ${formatSlotWindow(o.slot!.startAt, o.slot!.endAt)}'),
                                    if (o.totalPaise != null) Text(formatRupees(o.totalPaise!)),
                                  ],
                                ),
                                trailing: const Icon(Icons.chevron_right),
                              ),
                            );
                          },
                        ),
                      ),
              ),
            ),
    );
  }
}
