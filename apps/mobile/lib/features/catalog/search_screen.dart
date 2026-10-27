// Search: suggestions while typing (server trigram match on names and local names), results grid on submit.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/models/catalog.dart';
import '../../core/search_text.dart';
import '../../state/catalog_state.dart';
import '../../widgets/states.dart';
import 'product_grid.dart';

class SearchScreen extends ConsumerStatefulWidget {
  const SearchScreen({super.key, this.initialQuery});

  final String? initialQuery;

  @override
  ConsumerState<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends ConsumerState<SearchScreen> {
  late final _field = TextEditingController(text: widget.initialQuery ?? '');
  late String _typed = widget.initialQuery ?? '';
  late String? _submitted = widget.initialQuery;

  @override
  void dispose() {
    _field.dispose();
    super.dispose();
  }

  // Only a search the server accepts is sent (D235); anything else leaves the hint on screen.
  void _submit(String q) {
    if (!isSearchable(q)) return;
    FocusScope.of(context).unfocus();
    setState(() => _submitted = q.trim());
  }

  @override
  Widget build(BuildContext context) {
    final showResults = _submitted != null && _submitted == _typed.trim();
    final c = illustratedColors(context);
    final pill = OutlineInputBorder(
      borderRadius: BorderRadius.circular(FreshLayout.pillRadius),
      borderSide: BorderSide(color: c.line, width: 1.5),
    );
    return Scaffold(
      appBar: FreshPageHeader(
        key: const Key('pageHeader'),
        leading: const BackCircleButton(),
        title: TextField(
          key: const Key('searchField'),
          controller: _field,
          autofocus: widget.initialQuery == null,
          textInputAction: TextInputAction.search,
          inputFormatters: [LengthLimitingTextInputFormatter(searchMaxLength)],
          decoration: InputDecoration(
            hintText: 'Search fish, prawns, crabs…',
            isDense: true,
            prefixIcon: Icon(Icons.search_rounded, color: c.muted),
            border: pill,
            enabledBorder: pill,
            focusedBorder: pill.copyWith(borderSide: BorderSide(color: c.ink, width: 2)),
            fillColor: c.paper,
            suffixIcon: _typed.isEmpty
                ? null
                : IconButton(
                    tooltip: 'Clear search',
                    icon: const Icon(Icons.close),
                    onPressed: () => setState(() {
                      _field.clear();
                      _typed = '';
                      _submitted = null;
                    }),
                  ),
          ),
          onChanged: (v) => setState(() => _typed = v),
          onSubmitted: _submit,
        ),
      ),
      body: showResults
          ? ProductGrid(
              query: ProductQuery(q: _submitted),
              empty: EmptyState(
                slot: IllustrationSlot.noSearchResults,
                title: 'No results for "$_submitted"',
                message: 'Try a local name, such as vanjaram or eral, or check the spelling.',
              ),
            )
          : _Suggestions(query: _typed, onSearch: _submit),
    );
  }
}

class _Suggestions extends ConsumerWidget {
  const _Suggestions({required this.query, required this.onSearch});

  final String query;
  final ValueChanged<String> onSearch;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (!isSearchable(query)) {
      return const EmptyState(
        slot: IllustrationSlot.noSearchResults,
        title: 'Search the catch',
        message: 'Type at least two letters or numbers. Local names work too.',
      );
    }
    final suggestions = ref.watch(searchSuggestionsProvider(query));
    return AsyncValueView<List<SearchSuggestion>>(
      value: suggestions,
      onRetry: () => ref.invalidate(searchSuggestionsProvider(query)),
      loading: const ListSkeleton(rows: 4, rowHeight: 48),
      data: (items) => ListView(
        padding: FreshLayout.list,
        children: [
          ListTile(
            leading: const Icon(Icons.search),
            title: Text('Search for "${query.trim()}"'),
            onTap: () => onSearch(query),
          ),
          for (final s in items)
            ListTile(
              leading: SizedBox(width: 48, child: ProductPhoto(url: s.imageUrl, radius: 12)),
              title: Text(s.name),
              subtitle: s.matchedLocalName == null ? null : Text('Also called ${s.matchedLocalName}'),
              onTap: () => context.push('/product/${s.slug}'),
            ),
        ],
      ),
    );
  }
}
