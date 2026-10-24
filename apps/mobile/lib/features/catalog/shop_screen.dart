// Shop (D88): round category bubbles, filter pills (sort, in stock, source; also in a bottom sheet) and the
// two-column grid of product cards with ADD buttons. Filters are
// query parameters for the server; nothing is filtered or sorted on the device.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/catalog.dart';
import '../../state/catalog_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/bold.dart';
import '../../widgets/heading.dart';
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

  Future<void> _openSort() async {
    final chosen = await showModalBottomSheet<String>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: RadioGroup<String>(
          groupValue: _query.sort,
          onChanged: (v) => Navigator.pop(context, v),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [for (final e in _sorts.entries) RadioListTile<String>(value: e.key, title: Text(e.value))],
          ),
        ),
      ),
    );
    if (chosen != null) setState(() => _query = _query.copyWith(sort: chosen));
  }

  void _toggleSource(String code) => setState(
    () =>
        _query = _query.sourceType == code ? _query.copyWith(clearSourceType: true) : _query.copyWith(sourceType: code),
  );

  @override
  Widget build(BuildContext context) {
    final c = illustratedColors(context);
    final categories = ref.watch(categoriesProvider).value ?? const <Category>[];
    final tints = softTints(c);
    final header = Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 14),
          child: Semantics(
            button: true,
            label: 'Search fish, prawns, crabs',
            excludeSemantics: true,
            child: Material(
              color: c.paper,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(14),
                side: BorderSide(color: c.line, width: 1.5),
              ),
              child: InkWell(
                borderRadius: BorderRadius.circular(14),
                onTap: () => context.push('/search'),
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                  child: Row(
                    children: [
                      Icon(Icons.search_rounded, size: 18, color: c.muted),
                      const SizedBox(width: 8),
                      Text('Search “seer fish”, “prawns”…', style: TextStyle(color: c.muted, fontSize: 13)),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
        SizedBox(
          height: 88,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.fromLTRB(10, 10, 10, 0),
            children: [
              CategoryBubble(
                label: 'All',
                icon: Icons.apps_rounded,
                tint: tints[0],
                selected: _query.category == null,
                onTap: () => setState(() => _query = _query.copyWith(clearCategory: true)),
              ),
              for (var i = 0; i < categories.length; i++)
                CategoryBubble(
                  label: categories[i].name,
                  imageUrl: categories[i].imageUrl,
                  tint: tints[(i + 1) % tints.length],
                  selected: _query.category == categories[i].slug,
                  onTap: () => setState(() => _query = _query.copyWith(category: categories[i].slug)),
                ),
            ],
          ),
        ),
        SizedBox(
          height: 46,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.fromLTRB(14, 8, 14, 2),
            children: [
              FilterPill(
                key: const Key('sortPill'),
                icon: Icons.sort_rounded,
                label: _query.sort == 'relevance' ? 'Sort' : _sorts[_query.sort] ?? 'Sort',
                onTap: _openSort,
              ),
              const SizedBox(width: 6),
              FilterPill(
                label: 'In stock',
                selected: _query.inStock == true,
                onTap: () => setState(
                  () => _query = _query.inStock == true
                      ? _query.copyWith(clearInStock: true)
                      : _query.copyWith(inStock: true),
                ),
              ),
              for (final e in _sources.entries) ...[
                const SizedBox(width: 6),
                FilterPill(label: e.value, selected: _query.sourceType == e.key, onTap: () => _toggleSource(e.key)),
              ],
            ],
          ),
        ),
      ],
    );
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(18, 4, 6, 6),
              child: Row(
                children: [
                  const Expanded(
                    child: ScriptTitle(text: 'Shop', script: 'fresh'),
                  ),
                  const PincodeChip(),
                  RoundIconButton(
                    icon: Icons.tune_rounded,
                    tooltip: _activeFilters == 0 ? 'Filters' : 'Filters, $_activeFilters on',
                    onPressed: _openFilters,
                  ),
                ],
              ),
            ),
            Expanded(
              child: ProductGrid(
                query: _query,
                header: header,
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
