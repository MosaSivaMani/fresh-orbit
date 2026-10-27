// Addresses (docs/api §4.2): list with default, add, edit (If-Match), delete, make default. Field errors from
// the server's Problem Details are shown at the fields.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fresh_theme/fresh_theme.dart';
import 'package:go_router/go_router.dart';

import '../../api/api_error.dart';
import '../../api/models/account.dart';
import '../../core/format.dart';
import '../../state/account_state.dart';
import '../../widgets/states.dart';

class AddressesScreen extends ConsumerWidget {
  const AddressesScreen({super.key});

  Future<void> _delete(BuildContext context, WidgetRef ref, Address a) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete this address?'),
        content: Text(a.oneLine),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Keep')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Delete')),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await ref.read(addressesProvider.notifier).delete(a.id);
    } catch (e) {
      if (context.mounted) showErrorSnack(context, e);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: const PageHeader(text: 'Your', brushed: 'addresses'),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push('/account/addresses/new'),
        icon: const Icon(Icons.add),
        label: const Text('Add address'),
      ),
      body: AsyncValueView<List<Address>>(
        value: ref.watch(addressesProvider),
        onRetry: () => ref.invalidate(addressesProvider),
        data: (list) => list.isEmpty
            ? const EmptyState(
                slot: IllustrationSlot.emptyAddresses,
                title: 'No saved addresses',
                message: 'Add an address to choose it quickly at checkout.',
              )
            : ListView.separated(
                padding: const EdgeInsets.fromLTRB(FreshLayout.gutter, 0, FreshLayout.gutter, 96),
                itemCount: list.length,
                separatorBuilder: (_, _) => const SizedBox(height: FreshLayout.cardGap),
                itemBuilder: (context, i) {
                  final a = list[i];
                  return SectionBox(
                    key: ValueKey('address-${a.id}'),
                    child: Padding(
                      padding: const EdgeInsets.all(4),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Text(humanizeCode(a.label), style: AppText.display(illustratedColors(context).ink, 17)),
                              const SizedBox(width: FreshSpacing.s2),
                              if (a.isDefault) const FreshBadge(label: 'Default', tone: BannerTone.success),
                              if (a.isServiceable == false)
                                const FreshBadge(label: 'Not serviceable', tone: BannerTone.danger),
                            ],
                          ),
                          Text('${a.recipientName} · ${formatPhone(a.phone)}'),
                          Text(a.oneLine),
                          Wrap(
                            children: [
                              TextButton(
                                onPressed: () => context.push('/account/addresses/${a.id}'),
                                child: const Text('Edit'),
                              ),
                              if (!a.isDefault)
                                TextButton(
                                  onPressed: () =>
                                      ref.read(addressesProvider.notifier).makeDefault(a.id).catchError((Object e) {
                                        if (context.mounted) showErrorSnack(context, e);
                                      }),
                                  child: const Text('Make default'),
                                ),
                              TextButton(onPressed: () => _delete(context, ref, a), child: const Text('Delete')),
                            ],
                          ),
                        ],
                      ),
                    ),
                  );
                },
              ),
      ),
    );
  }
}

class AddressFormScreen extends ConsumerStatefulWidget {
  const AddressFormScreen({super.key, this.addressId});

  /// Null = new address.
  final String? addressId;

  @override
  ConsumerState<AddressFormScreen> createState() => _AddressFormScreenState();
}

class _AddressFormScreenState extends ConsumerState<AddressFormScreen> {
  final _form = GlobalKey<FormState>();
  final _c = <String, TextEditingController>{
    for (final f in [
      'recipientName',
      'phone',
      'line1',
      'line2',
      'landmark',
      'locality',
      'cityName',
      'stateCode',
      'pincode',
    ])
      f: TextEditingController(),
  };
  String _label = 'HOME';
  bool _isDefault = false;
  bool _saving = false;
  Map<String, String> _serverErrors = {};
  Address? _existing;
  bool _loaded = false;

  @override
  void dispose() {
    for (final c in _c.values) {
      c.dispose();
    }
    super.dispose();
  }

  void _fill(Address a) {
    _existing = a;
    _label = a.label;
    _isDefault = a.isDefault;
    _c['recipientName']!.text = a.recipientName;
    _c['phone']!.text = a.phone.startsWith('+91') ? a.phone.substring(3) : a.phone;
    _c['line1']!.text = a.line1;
    _c['line2']!.text = a.line2 ?? '';
    _c['landmark']!.text = a.landmark ?? '';
    _c['locality']!.text = a.locality;
    _c['cityName']!.text = a.cityName;
    _c['stateCode']!.text = a.stateCode;
    _c['pincode']!.text = a.pincode;
  }

  String? _opt(String f) => _c[f]!.text.trim().isEmpty ? null : _c[f]!.text.trim();

  Future<void> _save() async {
    setState(() => _serverErrors = {});
    if (!_form.currentState!.validate()) return;
    final input = AddressInput(
      label: _label,
      recipientName: _c['recipientName']!.text.trim(),
      phone: toIndianE164(_c['phone']!.text)!,
      line1: _c['line1']!.text.trim(),
      line2: _opt('line2'),
      landmark: _opt('landmark'),
      locality: _c['locality']!.text.trim(),
      cityName: _c['cityName']!.text.trim(),
      stateCode: _c['stateCode']!.text.trim().toUpperCase(),
      pincode: _c['pincode']!.text.trim(),
      isDefault: _isDefault,
    );
    setState(() => _saving = true);
    try {
      final notifier = ref.read(addressesProvider.notifier);
      final saved = _existing == null ? await notifier.add(input) : await notifier.replace(_existing!, input);
      if (mounted) context.pop(saved);
    } on ApiError catch (e) {
      if (!mounted) return;
      if (e.code == 'CONCURRENCY_CONFLICT') {
        showInfoSnack(context, 'This address changed since you opened it. We loaded the latest version.');
        ref.invalidate(addressesProvider);
        setState(() => _loaded = false);
      } else if (e.errors.isNotEmpty) {
        setState(() => _serverErrors = {for (final k in e.errors.keys) k: e.errors[k]!.first.message});
        _form.currentState!.validate();
      } else {
        showErrorSnack(context, e);
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Widget _field(
    String name,
    String label, {
    bool required = true,
    TextInputType? keyboard,
    List<TextInputFormatter>? formatters,
    String? Function(String)? check,
    String? prefix,
    Iterable<String>? autofill,
  }) => Padding(
    padding: const EdgeInsets.only(bottom: FreshSpacing.s3),
    child: TextFormField(
      controller: _c[name],
      keyboardType: keyboard,
      inputFormatters: formatters,
      autofillHints: autofill,
      decoration: InputDecoration(labelText: required ? label : '$label (optional)', prefixText: prefix),
      validator: (v) {
        final value = (v ?? '').trim();
        if (_serverErrors[name] != null) return _serverErrors[name];
        if (required && value.isEmpty) return 'Enter the ${label.toLowerCase()}.';
        if (value.isNotEmpty && check != null) return check(value);
        return null;
      },
    ),
  );

  @override
  Widget build(BuildContext context) {
    final id = widget.addressId;
    if (id != null && !_loaded) {
      final list = ref.watch(addressesProvider);
      final match = list.value?.where((a) => a.id == id).firstOrNull;
      if (match != null) {
        _fill(match);
        _loaded = true;
      } else if (list.hasError || (list.hasValue && match == null)) {
        return Scaffold(
          appBar: const PageHeader(text: 'Edit', brushed: 'address'),
          body: list.hasError
              ? ErrorState(error: list.error!, onRetry: () => ref.invalidate(addressesProvider))
              : const EmptyState(slot: IllustrationSlot.notFound, title: 'Address not found'),
        );
      } else {
        return const Scaffold(
          appBar: PageHeader(text: 'Edit', brushed: 'address'),
          body: ListSkeleton(rows: 4),
        );
      }
    }
    return Scaffold(
      appBar: PageHeader(text: id == null ? 'New' : 'Edit', brushed: 'address'),
      body: Form(
        key: _form,
        child: AutofillGroup(
          child: ListView(
            padding: FreshLayout.list,
            children: [
              Wrap(
                spacing: FreshSpacing.s2,
                children: [
                  for (final l in const ['HOME', 'WORK', 'OTHER'])
                    ChoiceChip(
                      label: Text(humanizeCode(l)),
                      selected: _label == l,
                      onSelected: (_) => setState(() => _label = l),
                    ),
                ],
              ),
              const SizedBox(height: FreshSpacing.s3),
              _field('recipientName', 'Name', autofill: const [AutofillHints.name]),
              _field(
                'phone',
                'Mobile number',
                keyboard: TextInputType.phone,
                prefix: '+91 ',
                formatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(10)],
                check: (v) => toIndianE164(v) == null ? 'Enter a 10-digit mobile number.' : null,
                autofill: const [AutofillHints.telephoneNumberNational],
              ),
              _field('line1', 'House / flat and street', autofill: const [AutofillHints.streetAddressLine1]),
              _field('line2', 'Apartment or area', required: false, autofill: const [AutofillHints.streetAddressLine2]),
              _field('landmark', 'Landmark', required: false),
              _field('locality', 'Locality', autofill: const [AutofillHints.sublocality]),
              _field('cityName', 'City', autofill: const [AutofillHints.addressCity]),
              _field(
                'stateCode',
                'State code (for example KA)',
                formatters: [
                  FilteringTextInputFormatter.allow(RegExp('[A-Za-z]')),
                  LengthLimitingTextInputFormatter(2),
                ],
                check: (v) => v.length != 2 ? 'Enter the 2-letter state code.' : null,
              ),
              _field(
                'pincode',
                'Pincode',
                keyboard: TextInputType.number,
                formatters: [FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(6)],
                check: (v) => isPincodeShape(v) ? null : 'Enter a 6-digit pincode.',
                autofill: const [AutofillHints.postalCode],
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Use as my default address'),
                value: _isDefault,
                onChanged: (v) => setState(() => _isDefault = v),
              ),
              const SizedBox(height: FreshSpacing.s3),
              PrimaryButton(label: 'Save address', loading: _saving, onPressed: _save),
            ],
          ),
        ),
      ),
    );
  }
}
