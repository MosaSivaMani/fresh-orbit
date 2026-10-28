// My orders (paged), order detail, tracking, and order actions shown from the server's `allowedActions`.

import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../api/models/orders.dart';
import 'cart_state.dart';
import 'core_providers.dart';
import 'session.dart';

class OrdersState {
  const OrdersState({required this.items, required this.totalCount, required this.page, this.loadingMore = false});

  final List<OrderSummary> items;
  final int totalCount;
  final int page;
  final bool loadingMore;

  bool get hasMore => items.length < totalCount;
}

final ordersProvider = AsyncNotifierProvider<OrdersController, OrdersState>(OrdersController.new);

class OrdersController extends AsyncNotifier<OrdersState> {
  @override
  Future<OrdersState> build() async {
    if (!ref.watch(sessionProvider.select((s) => s.signedIn))) {
      return const OrdersState(items: [], totalCount: 0, page: 1);
    }
    final page = await ref.read(orderRepositoryProvider).orders();
    return OrdersState(items: page.items, totalCount: page.totalCount, page: 1);
  }

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || current.loadingMore || !current.hasMore) return;
    state = AsyncData(
      OrdersState(items: current.items, totalCount: current.totalCount, page: current.page, loadingMore: true),
    );
    try {
      final next = await ref.read(orderRepositoryProvider).orders(page: current.page + 1);
      state = AsyncData(
        OrdersState(items: [...current.items, ...next.items], totalCount: next.totalCount, page: current.page + 1),
      );
    } catch (_) {
      state = AsyncData(current);
      rethrow;
    }
  }
}

final orderDetailProvider = FutureProvider.autoDispose.family<OrderDetail, String>(
  (ref, orderId) => ref.read(orderRepositoryProvider).order(orderId),
);

final orderTrackingProvider = FutureProvider.autoDispose.family<OrderTracking, String>(
  (ref, orderId) => ref.read(orderRepositoryProvider).tracking(orderId),
);

final orderComplaintsProvider = FutureProvider.autoDispose.family<List<Complaint>, String>(
  (ref, orderId) => ref.read(orderRepositoryProvider).complaints(orderId),
);

/// Order actions. Each user action gets its own Idempotency-Key, created when the customer confirms.
final orderActionsProvider = Provider<OrderActions>(OrderActions.new);

class OrderActions {
  OrderActions(this._ref);

  final Ref _ref;

  Future<void> cancel(String orderId, String reasonCode, {required String idempotencyKey}) async {
    await _ref.read(orderRepositoryProvider).cancel(orderId, reasonCode: reasonCode, idempotencyKey: idempotencyKey);
    _refresh(orderId);
  }

  /// Raises the complaint (JSON, `Idempotency-Key`). Photos go to it afterwards with [addComplaintPhoto], so a
  /// failed photo never raises the complaint a second time (D266).
  Future<Complaint> raiseComplaint(
    String orderId, {
    required String orderItemId,
    required String kind,
    required String description,
    required String idempotencyKey,
  }) async {
    final complaint = await _ref
        .read(orderRepositoryProvider)
        .raiseComplaint(
          orderId,
          orderItemId: orderItemId,
          kind: kind,
          description: description,
          idempotencyKey: idempotencyKey,
        );
    _ref.invalidate(orderComplaintsProvider(orderId));
    _refresh(orderId);
    return complaint;
  }

  /// One photo to a raised complaint (multipart `file`); the server checks type, size and the photo limit.
  Future<void> addComplaintPhoto(String orderId, String complaintId, ComplaintPhotoUpload photo) async {
    await _ref
        .read(orderRepositoryProvider)
        .addComplaintPhoto(orderId, complaintId, bytes: photo.bytes, fileName: photo.fileName);
    _ref.invalidate(orderComplaintsProvider(orderId));
  }

  /// Accepts or rejects a proposed substitution; the server answers with the updated order.
  Future<void> answerSubstitution(String orderId, String substitutionId, {required bool accept}) async {
    await _ref.read(orderRepositoryProvider).answerSubstitution(orderId, substitutionId, accept: accept);
    _refresh(orderId);
  }

  /// Adds the order's sellable lines to the cart; the server returns the priced cart.
  Future<void> reorder(String orderId) async {
    final cart = await _ref.read(orderRepositoryProvider).reorder(orderId);
    _ref.read(cartProvider.notifier).replace(cart);
  }

  void _refresh(String orderId) {
    _ref.invalidate(orderDetailProvider(orderId));
    _ref.invalidate(orderTrackingProvider(orderId));
    _ref.invalidate(ordersProvider);
  }

  static String newKey() => newIdempotencyKey();
}

/// A photo picked for a complaint, sent after the complaint is created.
class ComplaintPhotoUpload {
  const ComplaintPhotoUpload({required this.bytes, required this.fileName});

  final Uint8List bytes;
  final String fileName;
}
