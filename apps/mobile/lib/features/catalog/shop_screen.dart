// Shop: category chips, filters (source, in stock) in a bottom sheet, sort, and the paged grid. Filters are
// query parameters for the server; nothing is filtered or sorted on the device.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/catalog.dart';
import '../../state/catalog_state.dart';
import 'home_screen.dart';
import 'product_grid.dart';

const _sorts = <String, String>{
  'relevance': 'Recommended',
  'price': 'Price: low to high',
  '-price': 'Price: high to low',
  'name': 'Name',
  '-newest': 'Newest',
};

const _sources = <String, String>{'WILD': 'Wild caught', 'FARMED': 'Farmed'};

class ShopScreen extends ConsumerStatefulWidget {
  const ShopScreen({super.key, this.category});

  final String? category;

  @override
  ConsumerState<ShopScreen> createState() => _ShopScreenState();
}

class _ShopScreenState extends ConsumerState<ShopScreen> {
  late ProductQuery _query = ProductQuery(category: widget.category);

  @override
  void didUpdateWidget(ShopScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.category != widget.category) {
      _query = widget.category == null
          ? _query.copyWith(clearCategory: true)
          : _query.copyWith(category: widget.category);
    }
  }

  int get _activeFilters => (_query.sourceType == null ? 0 : 1) + (_query.inStock == true ? 1 : 0);

  Future<void> _openFilters() async {
    final result = await showModalBottomSheet<ProductQuery>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (context) => _FilterSheet(initial: _query),
    );
    if (result != null) setState(() => _query = result);
  }

  @override
  Widget build(BuildContext context) {
    final categories = ref.watch(categoriesProvider).value ?? const <Category>[];
    return Scaffold(
      appBar: AppBar(
        title: const PincodeChip(),
        actions: [
          IconButton(tooltip: 'Search', icon: const Icon(Icons.search), onPressed: () => context.push('/search')),
        ],
      ),
      body: Column(
        children: [
          SizedBox(
            height: 56,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s4),
              children: [
                Padding(
                  padding: const EdgeInsets.only(right: FreshSpacing.s2),
                  child: ChoiceChip(
                    label: const Text('All'),
                    selected: _query.category == null,
                    onSelected: (_) => setState(() => _query = _query.copyWith(clearCategory: true)),
                  ),
                ),
                for (final c in categories)
                  Padding(
                    padding: const EdgeInsets.only(right: FreshSpacing.s2),
                    child: ChoiceChip(
                      label: Text(c.name),
                      selected: _query.category == c.slug,
                      onSelected: (_) => setState(() => _query = _query.copyWith(category: c.slug)),
                    ),
                  ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: FreshSpacing.s4),
            child: Row(
              children: [
                OutlinedButton.icon(
                  onPressed: _openFilters,
                  icon: const Icon(Icons.tune),
                  label: Text(_activeFilters == 0 ? 'Filters' : 'Filters ($_activeFilters)'),
                ),
                const Spacer(),
                Flexible(
                  flex: 3,
                  child: DropdownButtonHideUnderline(
                    child: DropdownButton<String>(
                      isExpanded: true,
                      value: _query.sort,
                      alignment: AlignmentDirectional.centerEnd,
                      items: [
                        for (final e in _sorts.entries) DropdownMenuItem(value: e.key, child: Text(e.value)),
                      ],
                      onChanged: (v) => setState(() => _query = _query.copyWith(sort: v)),
                    ),
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            child: ProductGrid(
              query: _query,
              empty: _activeFilters > 0 || _query.category != null
                  ? Center(
                      child: TextButton(
                        onPressed: () => setState(() => _query = ProductQuery(sort: _query.sort)),
                        child: const Text('No matches. Clear filters'),
                      ),
                    )
                  : null,
            ),
          ),
        ],
      ),
    );
  }
}

class _FilterSheet extends StatefulWidget {
  const _FilterSheet({required this.initial});

  final ProductQuery initial;

  @override
  State<_FilterSheet> createState() => _FilterSheetState();
}

class _FilterSheetState extends State<_FilterSheet> {
  late String? _source = widget.initial.sourceType;
  late bool _inStock = widget.initial.inStock ?? false;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(FreshSpacing.s4, 0, FreshSpacing.s4, FreshSpacing.s4),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Semantics(header: true, child: Text('Filters', style: theme.textTheme.titleLarge)),
            const SizedBox(height: FreshSpacing.s4),
            Text('Source', style: theme.textTheme.titleSmall),
            Wrap(
              spacing: FreshSpacing.s2,
              children: [
                for (final e in _sources.entries)
                  FilterChip(
                    label: Text(e.value),
                    selected: _source == e.key,
                    onSelected: (on) => setState(() => _source = on ? e.key : null),
                  ),
              ],
            ),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Only show items in stock'),
              value: _inStock,
              onChanged: (v) => setState(() => _inStock = v),
            ),
            const SizedBox(height: FreshSpacing.s3),
            Row(
              children: [
                TextButton(
                  onPressed: () => setState(() {
                    _source = null;
                    _inStock = false;
                  }),
                  child: const Text('Clear all'),
                ),
                const Spacer(),
                FilledButton(
                  onPressed: () => Navigator.pop(
                    context,
                    widget.initial.copyWith(
                      sourceType: _source,
                      clearSourceType: _source == null,
                      inStock: _inStock ? true : null,
                      clearInStock: !_inStock,
                    ),
                  ),
                  child: const Text('Show results'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
