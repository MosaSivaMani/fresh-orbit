import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fresh_orbit_app/api/models/catalog.dart';
import 'package:fresh_orbit_app/illustrations/illustration.dart';
import 'package:fresh_orbit_app/theme/app_theme.dart';
import 'package:fresh_orbit_app/widgets/bold.dart';
import 'package:fresh_orbit_app/widgets/commerce.dart';
import 'package:fresh_orbit_app/widgets/heading.dart';
import 'package:fresh_orbit_app/widgets/illustrated.dart';
import 'package:fresh_theme/fresh_theme.dart';

Future<void> _pump(
  WidgetTester tester,
  Widget child, {
  bool reduceMotion = false,
  ThemeData? theme,
  bool scroll = true,
}) async {
  tester.view.devicePixelRatio = 3;
  tester.view.physicalSize = const Size(400 * 3, 1600 * 3);
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      theme: theme ?? AppTheme.light(),
      builder: (context, app) => MediaQuery(
        data: MediaQuery.of(context).copyWith(disableAnimations: reduceMotion),
        child: app!,
      ),
      home: Scaffold(body: scroll ? SingleChildScrollView(child: child) : child),
    ),
  );
}

ProductSummary _product({bool locationRequired = false, bool priced = true}) => ProductSummary.fromJson({
  'id': 'p1',
  'slug': 'seer-fish-vanjaram',
  'name': 'Seer Fish',
  'localNames': ['Vanjaram'],
  'sourceType': 'WILD',
  'imageUrl': null,
  'fromPrice': priced ? {'unitPricePaise': 129900, 'compareAtPaise': 149900, 'perUnit': 'KG'} : null,
  'availability': {'status': 'IN_STOCK', 'message': null},
  'locationRequired': locationRequired,
});

void main() {
  setUp(() => IllustrationAssets.debugSetAvailable(const {}));
  tearDown(() => IllustrationAssets.debugSetAvailable(null));

  group('AvatarCircle', () {
    testWidgets('draws the slot inside a circle of the asked size, decorative by default', (tester) async {
      final handle = tester.ensureSemantics();
      await _pump(tester, const Center(child: AvatarCircle(slot: IllustrationSlot.emptyCart, size: 150)));

      expect(tester.getSize(find.byKey(const ValueKey('avatar-EMPTY_CART'))), const Size(150, 150));
      expect(find.byKey(const ValueKey('illustration-placeholder-EMPTY_CART')), findsOneWidget);
      expect(find.byType(ClipOval), findsOneWidget);
      expect(find.bySemanticsLabel(IllustrationSlot.emptyCart.semanticLabel), findsNothing);
      handle.dispose();
    });

    testWidgets('a full-scene JPEG is cropped by the circle, never framed as a rectangle', (tester) async {
      IllustrationAssets.debugSetAvailable(const {'assets/illustrations/empty_cart.jpg'});
      await _pump(tester, const Center(child: AvatarCircle(slot: IllustrationSlot.emptyCart)));

      final image = tester.widget<Image>(find.byKey(const ValueKey('illustration-image-EMPTY_CART')));
      expect((image.image as AssetImage).assetName, 'assets/illustrations/empty_cart.jpg');
      expect(image.fit, BoxFit.cover);
      expect(find.byType(ClipOval), findsOneWidget);
      expect(find.byType(ClipRRect), findsNothing);
    });

    testWidgets('a bare JPEG Illustration keeps its rounded frame', (tester) async {
      IllustrationAssets.debugSetAvailable(const {'assets/illustrations/empty_cart.jpg'});
      await _pump(tester, const Illustration(slot: IllustrationSlot.emptyCart));

      expect(find.byType(ClipRRect), findsOneWidget);
    });
  });

  group('ColorBlockScreen', () {
    Widget screen({VoidCallback? onBack}) => ColorBlockScreen(
      tone: BlockTone.coral,
      eyebrow: 'Fresh Orbit',
      heading: const [HeadingLine('Straight off'), HeadingLine('the boats', script: true, brush: true)],
      slot: IllustrationSlot.onboardingFresh,
      onBack: onBack,
      sticker: const StickerSpot(left: 20, child: Sticker(label: 'Morning catch')),
      children: const [SheetTitle('Caught at dawn', header: true), SheetNote('Fresh fish.')],
    );

    testWidgets('shows the eyebrow, one header for the heading, avatar, sticker and sheet content', (tester) async {
      final handle = tester.ensureSemantics();
      var back = 0;
      await _pump(tester, screen(onBack: () => back++), scroll: false);
      await tester.pumpAndSettle();

      expect(find.text('FRESH ORBIT'), findsOneWidget);
      expect(find.bySemanticsLabel('Straight off the boats'), findsOneWidget);
      expect(find.byKey(const ValueKey('avatar-ONBOARDING_FRESH')), findsOneWidget);
      expect(find.text('Morning catch'), findsOneWidget);
      expect(find.text('Caught at dawn'), findsOneWidget);
      expect(find.byType(BrushSwipe), findsOneWidget);

      await tester.tap(find.byTooltip('Back'));
      expect(back, 1);
      handle.dispose();
    });

    testWidgets('with reduced motion the sheet is in place at once, with no running animation', (tester) async {
      await _pump(tester, screen(), reduceMotion: true, scroll: false);

      expect(tester.hasRunningAnimations, isFalse);
      expect(find.ancestor(of: find.text('Caught at dawn'), matching: find.byType(Opacity)), findsNothing);
    });

    testWidgets('renders in dark mode without layout errors', (tester) async {
      await _pump(tester, screen(), theme: AppTheme.dark(), scroll: false);
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });
  });

  group('Heading', () {
    testWidgets('script lines are tilted, brush lines sit on a swipe, and the doodle is optional', (tester) async {
      await _pump(
        tester,
        const Column(
          children: [
            Heading(lines: [HeadingLine('Cleaned'), HeadingLine('your way', script: true)], color: Colors.black),
            Heading(lines: [HeadingLine('Plain')], color: Colors.black, doodle: false),
          ],
        ),
      );

      int transforms(String text) =>
          find.ancestor(of: find.text(text), matching: find.byType(Transform)).evaluate().length;
      expect(transforms('your way'), transforms('Cleaned') + 1, reason: 'the script line is tilted');
      final script = tester.widget<Text>(find.text('your way'));
      expect(script.style!.fontFamily, AppFonts.script);
      expect(script.style!.fontSize, closeTo(35 * 1.12, 0.01));
      expect(find.byType(BrushSwipe), findsNothing);
      expect(
        find.descendant(of: find.byType(Heading).last, matching: find.byType(CustomPaint)),
        findsNothing,
        reason: 'no doodle when switched off',
      );
    });

    testWidgets('ScriptTitle reads as one header', (tester) async {
      final handle = tester.ensureSemantics();
      await _pump(tester, const ScriptTitle(text: 'Your', script: 'cart'));

      expect(find.bySemanticsLabel('Your cart'), findsOneWidget);
      expect(tester.widget<Text>(find.text('cart')).style!.fontFamily, AppFonts.script);
      handle.dispose();
    });
  });

  group('FoodTabBar', () {
    testWidgets('icon over label, selected tab announced, cart badge counted', (tester) async {
      final handle = tester.ensureSemantics();
      final tapped = <int>[];
      await _pump(
        tester,
        FoodTabBar(
          tabs: const [
            FoodTab('Home', Icons.home_outlined),
            FoodTab('Shop', Icons.grid_view_rounded),
            FoodTab('Cart', Icons.shopping_cart_outlined, badge: 2),
            FoodTab('Orders', Icons.receipt_long_outlined),
            FoodTab('Account', Icons.person_outline_rounded),
          ],
          currentIndex: 1,
          onSelected: tapped.add,
        ),
      );

      for (final label in ['Home', 'Shop', 'Cart', 'Orders', 'Account']) {
        expect(find.text(label), findsOneWidget);
      }
      expect(find.text('2'), findsOneWidget);
      expect(tester.getSemantics(find.byKey(const ValueKey('tab-Cart'))).label, 'Cart, 2 items');
      expect(tester.getSemantics(find.byKey(const ValueKey('tab-Shop'))), isSemantics(isSelected: true));
      expect(tester.getSemantics(find.byKey(const ValueKey('tab-Home'))), isSemantics(isSelected: false));
      // The icon sits above its label.
      expect(tester.getCenter(find.byIcon(Icons.home_outlined)).dy, lessThan(tester.getCenter(find.text('Home')).dy));

      await tester.tap(find.text('Orders'));
      expect(tapped, [3]);
      handle.dispose();
    });
  });

  testWidgets('the cart bar shows the count and the given amount, and opens the cart', (tester) async {
    final handle = tester.ensureSemantics();
    var opened = 0;
    await _pump(tester, FreshCartBar(count: 2, amount: '₹708.50', onTap: () => opened++));

    expect(find.text('2 ITEMS ADDED'), findsOneWidget);
    expect(find.text('₹708.50'), findsOneWidget);
    expect(find.bySemanticsLabel('2 items added, ₹708.50. View cart'), findsOneWidget);
    await tester.tap(find.byKey(const Key('viewCartBar')));
    expect(opened, 1);
    handle.dispose();
  });

  testWidgets('the cart bar shows no picture circles when no line has a picture', (tester) async {
    await _pump(tester, FreshCartBar(count: 2, amount: '₹708.50', onTap: () {}));

    expect(find.byKey(const ValueKey('cartThumb-0')), findsNothing);
    expect(find.byType(Image), findsNothing);
    expect(tester.takeException(), isNull);
  });

  group('ProductCard', () {
    testWidgets('ADD sits on the picture edge and adds', (tester) async {
      final handle = tester.ensureSemantics();
      var added = 0;
      await _pump(
        tester,
        SizedBox(
          width: 180,
          height: 250,
          child: ProductCard(product: _product(), onTap: () {}, control: CardAdd(() => added++)),
        ),
      );

      expect(find.text('Seer Fish'), findsOneWidget);
      expect(find.text('₹1,299/kg'), findsOneWidget);
      await tester.tap(find.bySemanticsLabel('Add Seer Fish to cart'));
      expect(added, 1);
      handle.dispose();
    });

    testWidgets('in the cart the button is a − n + stepper', (tester) async {
      final calls = <String>[];
      await _pump(
        tester,
        SizedBox(
          width: 180,
          height: 250,
          child: ProductCard(
            product: _product(),
            onTap: () {},
            control: CardStepper(label: '500 g', onDecrease: () => calls.add('-'), onIncrease: () => calls.add('+')),
          ),
        ),
        scroll: false,
      );

      expect(find.text('ADD'), findsNothing);
      expect(find.text('500 g'), findsOneWidget);
      await tester.tap(find.byTooltip('Increase quantity of Seer Fish'));
      await tester.tap(find.byTooltip('Decrease quantity of Seer Fish'));
      expect(calls, ['+', '-']);
    });

    testWidgets('without a price for the location the ADD is disabled', (tester) async {
      final handle = tester.ensureSemantics();
      await _pump(
        tester,
        SizedBox(
          width: 180,
          height: 250,
          child: ProductCard(product: _product(priced: false), onTap: () {}, control: const CardAdd(null)),
        ),
        scroll: false,
      );

      expect(find.text('₹1,299/kg'), findsNothing);
      expect(tester.getSemantics(find.bySemanticsLabel('Add Seer Fish to cart')), isSemantics(isEnabled: false));
      handle.dispose();
    });
  });

  testWidgets('category bubbles and filter pills announce selection and tap', (tester) async {
    final handle = tester.ensureSemantics();
    final taps = <String>[];
    await _pump(
      tester,
      Wrap(
        children: [
          CategoryBubble(label: 'Fish', tint: Colors.teal, selected: true, onTap: () => taps.add('fish')),
          CategoryBubble(label: 'Prawns', tint: Colors.orange, selected: false, onTap: () => taps.add('prawns')),
          FilterPill(label: 'In stock', selected: true, onTap: () => taps.add('stock')),
          FilterPill(label: 'Wild caught', onTap: () => taps.add('wild')),
        ],
      ),
    );

    expect(tester.getSemantics(find.bySemanticsLabel('Fish')), isSemantics(isSelected: true));
    expect(tester.getSemantics(find.bySemanticsLabel('Prawns')), isSemantics(isSelected: false));
    expect(tester.getSemantics(find.bySemanticsLabel('In stock')), isSemantics(isSelected: true));
    await tester.tap(find.text('Prawns'));
    await tester.tap(find.text('Wild caught'));
    expect(taps, ['prawns', 'wild']);
    handle.dispose();
  });

  testWidgets('segmented pills select one option', (tester) async {
    final picked = <ThemeMode>[];
    await _pump(
      tester,
      SegmentedPills<ThemeMode>(
        options: const {ThemeMode.light: 'Light', ThemeMode.system: 'Auto', ThemeMode.dark: 'Dark'},
        selected: ThemeMode.system,
        onSelected: picked.add,
        semanticLabel: 'Theme',
      ),
    );

    await tester.tap(find.text('Dark'));
    expect(picked, [ThemeMode.dark]);
  });

  testWidgets('slot buttons: unbookable slots cannot be chosen', (tester) async {
    final chosen = <String>[];
    await _pump(
      tester,
      Row(
        children: [
          SlotButton(title: 'Today', subtitle: '7–9 am', selected: true, onTap: () => chosen.add('a')),
          const SlotButton(title: 'Today', subtitle: 'Full', selected: false, onTap: null),
        ],
      ),
    );

    await tester.tap(find.text('Full'));
    await tester.tap(find.text('7–9 am'));
    expect(chosen, ['a']);
  });

  testWidgets('progress steps announce the reached step', (tester) async {
    final handle = tester.ensureSemantics();
    await _pump(tester, const ProgressSteps(labels: ['Placed', 'Packed', 'On the way', 'Delivered'], reached: 3));

    expect(find.bySemanticsLabel('Step 3 of 4: On the way'), findsOneWidget);
    handle.dispose();
  });

  testWidgets('teal action shows the given amount and is disabled without a handler', (tester) async {
    await _pump(
      tester,
      const StickyActionBar(
        action: TealActionButton(key: Key('go'), label: 'Place order', amount: '₹708.50', onPressed: null),
      ),
    );

    expect(find.text('Place order'), findsOneWidget);
    expect(find.text('₹708.50'), findsOneWidget);
    final button = tester.widget<FilledButton>(
      find.descendant(of: find.byKey(const Key('go')), matching: find.byType(FilledButton)),
    );
    expect(button.onPressed, isNull);
  });

  testWidgets('sticker keeps the mockup badge colours in dark mode', (tester) async {
    await _pump(
      tester,
      const Sticker(label: 'Kept cold', tone: StickerTone.teal),
      theme: AppTheme.dark(),
    );

    expect(find.text('Kept cold'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
