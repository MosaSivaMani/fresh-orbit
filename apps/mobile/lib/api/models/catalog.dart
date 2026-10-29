// TEMPORARY: replace with fresh_api generated models after Phase 3
//
// docs/api/README.md §4.4: GET /home, /categories, /products, /products/{slug}, /search/suggest.
// Nullable price/availability fields follow §1.4: without a pincode they are null and `locationRequired` is true.

import 'common.dart';
import 'json.dart';

/// `{ unitPricePaise, compareAtPaise, perUnit }`: perUnit KG, PIECE or PACK.
class FromPrice {
  const FromPrice({required this.unitPricePaise, this.compareAtPaise, required this.perUnit});

  factory FromPrice.fromJson(Json j) => FromPrice(
    unitPricePaise: reqInt(j, 'unitPricePaise'),
    compareAtPaise: optInt(j, 'compareAtPaise'),
    perUnit: reqString(j, 'perUnit'),
  );

  final int unitPricePaise;
  final int? compareAtPaise;
  final String perUnit;
}

class ProductSummary {
  const ProductSummary({
    required this.id,
    required this.slug,
    required this.name,
    required this.localNames,
    this.sourceType,
    this.imageUrl,
    this.fromPrice,
    this.availability,
    required this.locationRequired,
    this.freshness,
    this.defaultVariant,
  });

  factory ProductSummary.fromJson(Json j) {
    final price = asJsonOrNull(j['fromPrice'], 'fromPrice');
    final variant = asJsonOrNull(j['defaultVariant'], 'defaultVariant');
    return ProductSummary(
      defaultVariant: variant == null ? null : ListVariant.tryParse(variant),
      id: reqString(j, 'id'),
      slug: reqString(j, 'slug'),
      name: reqString(j, 'name'),
      localNames: stringList(j, 'localNames'),
      sourceType: optString(j, 'sourceType'),
      imageUrl: optString(j, 'imageUrl'),
      fromPrice: price == null ? null : FromPrice.fromJson(price),
      availability: optAvailability(j, 'availability'),
      locationRequired: optBool(j, 'locationRequired') ?? false,
      freshness: Freshness.fromJsonOrNull(j['freshness']),
    );
  }

  final String id;
  final String slug;
  final String name;
  final List<String> localNames;
  final String? sourceType;
  final String? imageUrl;
  final FromPrice? fromPrice;
  final Availability? availability;
  final bool locationRequired;

  /// The default variant's next batch (CAT-R25); null without a pincode or sellable stock.
  final Freshness? freshness;

  /// The size the card's "from" price belongs to and the one ADD puts in the cart (CAT-R23, MB-2); null only when
  /// the product has no visible size.
  final ListVariant? defaultVariant;
}

/// `defaultVariant` of a list item: the size, its order rules and its default cut, so ADD needs no detail call.
class ListVariant {
  const ListVariant({required this.id, required this.name, required this.minOrder, this.defaultPreparationCode});

  factory ListVariant.fromJson(Json j) => ListVariant(
    id: reqString(j, 'id'),
    name: reqString(j, 'name'),
    minOrder: reqQuantity(j, 'minOrder'),
    defaultPreparationCode: optString(j, 'defaultPreparationCode'),
  );

  /// Null when the shape is incomplete: the card then reads the product detail before adding, as before.
  static ListVariant? tryParse(Json j) {
    try {
      return ListVariant.fromJson(j);
    } on FormatException {
      return null;
    }
  }

  final String id;
  final String name;
  final Quantity minOrder;

  /// The cut the cart uses when none is sent; null when the size has no cuts.
  final String? defaultPreparationCode;
}

/// `{ caughtOn, receivedAt }` of the batch the next order is packed from (CAT-R25). Shown as given; nothing is
/// shown when it is null, and no catch time is guessed (D212, D240).
class Freshness {
  const Freshness({this.caughtOn, this.receivedAt});

  static Freshness? fromJsonOrNull(Object? value) {
    final j = asJsonOrNull(value, 'freshness');
    return j == null ? null : Freshness(caughtOn: optString(j, 'caughtOn'), receivedAt: optInstant(j, 'receivedAt'));
  }

  /// Calendar date `YYYY-MM-DD`; null when the catch date was not recorded at receipt.
  final String? caughtOn;
  final DateTime? receivedAt;
}

class Category {
  const Category({
    required this.id,
    required this.slug,
    required this.name,
    this.description,
    this.imageUrl,
    required this.children,
  });

  factory Category.fromJson(Json j) => Category(
    id: reqString(j, 'id'),
    slug: reqString(j, 'slug'),
    name: reqString(j, 'name'),
    description: optString(j, 'description'),
    imageUrl: optString(j, 'imageUrl'),
    children: listOrEmpty(j, 'children', Category.fromJson),
  );

  final String id;
  final String slug;
  final String name;
  final String? description;
  final String? imageUrl;
  final List<Category> children;
}

/// Home banner (Promotions `HomeBanner`). Kept loose on purpose. `linkType` is CATEGORY, PRODUCT or COUPON;
/// `linkSlug` is the category or product slug (D172), null for a coupon or a missing target. `badge` is the
/// admin's optional short text (D210), shown as given.
class HomeBanner {
  const HomeBanner({required this.title, this.subtitle, this.imageUrl, this.badge, this.linkType, this.linkSlug});

  factory HomeBanner.fromJson(Json j) => HomeBanner(
    title: optString(j, 'title') ?? '',
    subtitle: optString(j, 'subtitle') ?? optString(j, 'description'),
    imageUrl: optString(j, 'imageUrl'),
    badge: optString(j, 'badge'),
    linkType: optString(j, 'linkType'),
    linkSlug: optString(j, 'linkSlug'),
  );

  final String title;
  final String? subtitle;
  final String? imageUrl;
  final String? badge;
  final String? linkType;
  final String? linkSlug;

  /// Where a tap goes: `/shop?category=<slug>` or `/product/<slug>`; null when the banner links nowhere the app
  /// can open.
  String? get route {
    final slug = linkSlug;
    if (slug == null || slug.isEmpty) return null;
    return switch (linkType) {
      'CATEGORY' => Uri(path: '/shop', queryParameters: {'category': slug}).toString(),
      'PRODUCT' => '/product/${Uri.encodeComponent(slug)}',
      _ => null,
    };
  }
}

class HomeData {
  const HomeData({
    required this.banners,
    required this.featuredCategories,
    required this.featuredProducts,
    required this.locationRequired,
  });

  factory HomeData.fromJson(Json j) => HomeData(
    banners: listOrEmpty(j, 'banners', HomeBanner.fromJson),
    featuredCategories: listOrEmpty(j, 'featuredCategories', Category.fromJson),
    featuredProducts: listOrEmpty(j, 'featuredProducts', ProductSummary.fromJson),
    locationRequired: optBool(j, 'locationRequired') ?? false,
  );

  final List<HomeBanner> banners;
  final List<Category> featuredCategories;
  final List<ProductSummary> featuredProducts;
  final bool locationRequired;
}

class ProductImage {
  const ProductImage({required this.url, required this.altText});

  factory ProductImage.fromJson(Json j) =>
      ProductImage(url: reqString(j, 'url'), altText: optString(j, 'altText') ?? '');

  final String url;
  final String altText;
}

/// Preparation option with its yield and the location's charge (null charge = cannot be ordered there).
class Preparation {
  const Preparation({
    required this.code,
    required this.name,
    required this.yieldBp,
    required this.isDefault,
    this.chargePaise,
    this.chargeBasis,
  });

  factory Preparation.fromJson(Json j) => Preparation(
    code: reqString(j, 'code'),
    name: reqString(j, 'name'),
    yieldBp: reqInt(j, 'yieldBp'),
    isDefault: optBool(j, 'isDefault') ?? false,
    chargePaise: optInt(j, 'chargePaise'),
    chargeBasis: optString(j, 'chargeBasis'),
  );

  final String code;
  final String name;
  final int yieldBp;
  final bool isDefault;
  final int? chargePaise;

  /// PER_KG, PER_ITEM or FLAT.
  final String? chargeBasis;
}

class VariantAvailability {
  const VariantAvailability({required this.status, this.maxOrderableNow, this.message});

  factory VariantAvailability.fromJson(Json j) => VariantAvailability(
    status: reqString(j, 'status'),
    maxOrderableNow: optQuantity(j, 'maxOrderableNow'),
    message: optString(j, 'message'),
  );

  final String status;
  final Quantity? maxOrderableNow;
  final String? message;

  bool get canAdd => status == 'IN_STOCK' || status == 'LOW_STOCK';
}

class Variant {
  const Variant({
    required this.id,
    required this.name,
    required this.sellUnit,
    required this.isWeightVariable,
    required this.minOrder,
    required this.maxOrder,
    required this.orderStep,
    this.unitPricePaise,
    this.compareAtPaise,
    required this.preparations,
    this.availability,
    this.freshness,
    this.serving,
  });

  factory Variant.fromJson(Json j) {
    final availability = asJsonOrNull(j['availability'], 'availability');
    return Variant(
      id: reqString(j, 'id'),
      name: reqString(j, 'name'),
      sellUnit: reqString(j, 'sellUnit'),
      isWeightVariable: optBool(j, 'isWeightVariable') ?? false,
      minOrder: reqQuantity(j, 'minOrder'),
      maxOrder: reqQuantity(j, 'maxOrder'),
      orderStep: reqQuantity(j, 'orderStep'),
      unitPricePaise: optInt(j, 'unitPricePaise'),
      compareAtPaise: optInt(j, 'compareAtPaise'),
      preparations: listOrEmpty(j, 'preparations', Preparation.fromJson),
      availability: availability == null ? null : VariantAvailability.fromJson(availability),
      freshness: Freshness.fromJsonOrNull(j['freshness']),
      serving: Serving.fromJsonOrNull(j['serving']),
    );
  }

  final String id;
  final String name;

  /// KG, PIECE or PACK.
  final String sellUnit;
  final bool isWeightVariable;
  final Quantity minOrder;
  final Quantity maxOrder;
  final Quantity orderStep;
  final int? unitPricePaise;
  final int? compareAtPaise;
  final List<Preparation> preparations;
  final VariantAvailability? availability;

  /// This size's next batch (CAT-R25).
  final Freshness? freshness;

  /// Pieces and servings per serving basis, as the server states them (CAT-R21); null when not stated.
  final Serving? serving;

  Preparation? get defaultPreparation {
    for (final p in preparations) {
      if (p.isDefault) return p;
    }
    return preparations.isEmpty ? null : preparations.first;
  }
}

class ProductDetail {
  const ProductDetail({
    required this.id,
    required this.slug,
    required this.name,
    required this.localNames,
    this.description,
    this.sourceType,
    this.sourceRegion,
    this.storageInstructions,
    this.categoryName,
    required this.images,
    required this.variants,
    required this.netWeightNote,
    this.isWishlisted,
    required this.locationRequired,
    this.offer,
  });

  factory ProductDetail.fromJson(Json j) {
    final category = asJsonOrNull(j['category'], 'category');
    final offer = asJsonOrNull(j['offer'], 'offer');
    return ProductDetail(
      id: reqString(j, 'id'),
      slug: reqString(j, 'slug'),
      name: reqString(j, 'name'),
      localNames: stringList(j, 'localNames'),
      description: optString(j, 'description'),
      sourceType: optString(j, 'sourceType'),
      sourceRegion: optString(j, 'sourceRegion'),
      storageInstructions: optString(j, 'storageInstructions'),
      categoryName: category == null ? null : optString(category, 'name'),
      images: listOrEmpty(j, 'images', ProductImage.fromJson),
      variants: listOrEmpty(j, 'variants', Variant.fromJson),
      netWeightNote: optString(j, 'netWeightNote') ?? '',
      isWishlisted: optBool(j, 'isWishlisted'),
      locationRequired: optBool(j, 'locationRequired') ?? false,
      offer: offer == null ? null : ProductOffer.fromJson(offer),
    );
  }

  final String id;
  final String slug;
  final String name;
  final List<String> localNames;
  final String? description;
  final String? sourceType;
  final String? sourceRegion;
  final String? storageInstructions;
  final String? categoryName;
  final List<ProductImage> images;
  final List<Variant> variants;
  final String netWeightNote;
  final bool? isWishlisted;
  final bool locationRequired;

  /// The coupon line for the pincode's city (CAT-R26); null without a location or when none applies.
  final ProductOffer? offer;
}

/// `offer { couponCode, text }` on a product: the server's sentence, shown as sent (no amount worked out here).
class ProductOffer {
  const ProductOffer({required this.couponCode, required this.text});

  factory ProductOffer.fromJson(Json j) =>
      ProductOffer(couponCode: optString(j, 'couponCode') ?? '', text: optString(j, 'text') ?? '');

  final String couponCode;
  final String text;
}

/// `serving { basis, netWeightGrams, piecesMin, piecesMax, serves }` (CAT-R21): per one kg net, one pack or one
/// piece. Missing values stay null; the app never scales them to another quantity.
class Serving {
  const Serving({required this.basis, this.piecesMin, this.piecesMax, this.serves});

  static Serving? fromJsonOrNull(Object? value) {
    if (value is! Map) return null;
    final j = value.cast<String, dynamic>();
    final basis = optQuantity(j, 'basis');
    if (basis == null) return null;
    return Serving(
      basis: basis,
      piecesMin: optInt(j, 'piecesMin'),
      piecesMax: optInt(j, 'piecesMax'),
      serves: optInt(j, 'serves'),
    );
  }

  final Quantity basis;
  final int? piecesMin;
  final int? piecesMax;
  final int? serves;

  bool get isEmpty => piecesMin == null && serves == null;
}

/// One line priced by the server before it is added (GET /price-quote, CART-R24): the estimate for the chosen
/// size, cut and quantity and the most it can cost after weighing. The app shows these values, never its own.
class PriceQuote {
  const PriceQuote({
    required this.quantity,
    this.maxChargeableQuantity,
    required this.estimatedTotalPaise,
    this.maxChargeablePaise,
    this.availabilityStatus,
    this.availabilityMessage,
    this.weightNote,
  });

  factory PriceQuote.fromJson(Json j) {
    final availability = asJsonOrNull(j['availability'], 'availability');
    return PriceQuote(
      quantity: reqQuantity(j, 'quantity'),
      maxChargeableQuantity: optQuantity(j, 'maxChargeableQuantity'),
      estimatedTotalPaise: reqInt(j, 'estimatedTotalPaise'),
      maxChargeablePaise: optInt(j, 'maxChargeablePaise'),
      availabilityStatus: availability == null ? null : optString(availability, 'status'),
      availabilityMessage: availability == null ? null : optString(availability, 'message'),
      weightNote: optString(j, 'weightNote'),
    );
  }

  final Quantity quantity;
  final Quantity? maxChargeableQuantity;
  final int estimatedTotalPaise;
  final int? maxChargeablePaise;
  final String? availabilityStatus;
  final String? availabilityMessage;
  final String? weightNote;
}

/// The business's public profile (GET /business-profile): who runs the shop and how to reach support.
class BusinessProfile {
  const BusinessProfile({
    this.tradeName,
    this.legalName,
    this.supportPhone,
    this.supportEmail,
    this.supportHours,
    this.address,
    this.gstin,
    this.fssaiLicenseNo,
  });

  factory BusinessProfile.fromJson(Json j) => BusinessProfile(
    tradeName: optString(j, 'tradeName'),
    legalName: optString(j, 'legalName'),
    address: optString(j, 'address'),
    gstin: optString(j, 'gstin'),
    fssaiLicenseNo: optString(j, 'fssaiLicenseNo'),
    supportPhone: optString(j, 'supportPhone'),
    supportEmail: optString(j, 'supportEmail'),
    supportHours: optString(j, 'supportHours'),
  );

  final String? tradeName;
  final String? legalName;
  final String? supportPhone;
  final String? supportEmail;
  final String? supportHours;

  /// The registered address, GSTIN and FSSAI licence (legal details the store must show; null until set).
  final String? address;
  final String? gstin;
  final String? fssaiLicenseNo;
}

class SearchSuggestion {
  const SearchSuggestion({required this.name, this.matchedLocalName, required this.slug, this.imageUrl});

  factory SearchSuggestion.fromJson(Json j) => SearchSuggestion(
    name: reqString(j, 'name'),
    matchedLocalName: optString(j, 'matchedLocalName'),
    slug: reqString(j, 'slug'),
    imageUrl: optString(j, 'imageUrl'),
  );

  final String name;
  final String? matchedLocalName;
  final String slug;
  final String? imageUrl;
}

/// `GET /availability` item: live availability for one variant and preparation, with the server price.
class AvailabilityItem {
  const AvailabilityItem({
    required this.variantId,
    required this.status,
    this.preparationCode,
    this.maxOrderableNow,
    this.unitPricePaise,
    this.message,
  });

  factory AvailabilityItem.fromJson(Json j) => AvailabilityItem(
    variantId: reqString(j, 'variantId'),
    status: reqString(j, 'status'),
    preparationCode: optString(j, 'preparationCode'),
    maxOrderableNow: optQuantity(j, 'maxOrderableNow'),
    unitPricePaise: optInt(j, 'unitPricePaise'),
    message: optString(j, 'message'),
  );

  final String variantId;
  final String status;
  final String? preparationCode;
  final Quantity? maxOrderableNow;
  final int? unitPricePaise;
  final String? message;
}

/// Product list filters (query parameters of `GET /products`).
class ProductQuery {
  const ProductQuery({
    this.category,
    this.q,
    this.sourceType,
    this.preparation,
    this.inStock,
    this.sort = 'relevance',
    this.page = 1,
    this.pageSize = 20,
  });

  final String? category;
  final String? q;
  final String? sourceType;
  final String? preparation;
  final bool? inStock;

  /// relevance, price, -price, name, -newest. Relevance needs [q] (CAT-R15), so without one no sort is sent and
  /// the server uses its default order.
  final String sort;
  final int page;
  final int pageSize;

  Map<String, dynamic> toQuery() => {
    if (category != null) 'category': category,
    if (q != null && q!.isNotEmpty) 'q': q,
    if (sourceType != null) 'sourceType': sourceType,
    if (preparation != null) 'preparation': preparation,
    if (inStock != null) 'inStock': inStock,
    if (sort != 'relevance' || (q?.isNotEmpty ?? false)) 'sort': sort,
    'page': page,
    'pageSize': pageSize,
  };

  ProductQuery copyWith({
    String? category,
    String? q,
    String? sourceType,
    String? preparation,
    bool? inStock,
    String? sort,
    int? page,
    bool clearCategory = false,
    bool clearSourceType = false,
    bool clearInStock = false,
  }) => ProductQuery(
    category: clearCategory ? null : (category ?? this.category),
    q: q ?? this.q,
    sourceType: clearSourceType ? null : (sourceType ?? this.sourceType),
    preparation: preparation ?? this.preparation,
    inStock: clearInStock ? null : (inStock ?? this.inStock),
    sort: sort ?? this.sort,
    page: page ?? this.page,
    pageSize: pageSize,
  );

  @override
  bool operator ==(Object other) =>
      other is ProductQuery &&
      other.category == category &&
      other.q == q &&
      other.sourceType == sourceType &&
      other.preparation == preparation &&
      other.inStock == inStock &&
      other.sort == sort &&
      other.page == page &&
      other.pageSize == pageSize;

  @override
  int get hashCode => Object.hash(category, q, sourceType, preparation, inStock, sort, page, pageSize);
}
