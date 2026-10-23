#!/usr/bin/env bash
# End-to-end smoke test of a running Api + Worker (docs/runbooks/local-development.md).
#
#   API_URL=http://localhost:5080 API_LOG=/path/to/api.log \
#   ADMIN_EMAIL=admin@freshorbit.local ADMIN_PASSWORD='...' infra/scripts/smoke.sh
#
# Staff sets up a new city end to end (city, location, service area, pincode, hours, slot templates, category, product,
# variant with preparations, price, preparation charge, tax rules, stock), activates the city; a customer signs in by
# OTP (code read from the Api log written by the logging SMS sender), adds to cart, previews and places a COD order;
# staff packs, dispatches and delivers it with the customer's code; the script then checks the invoice, the COLLECTED
# payment, the consumed stock and the queued notifications. Every run uses fresh codes, so it can be re-run.
# Needs bash, curl, jq. Exit code 0 only when every step passes.
set -uo pipefail

API="${API_URL:-http://localhost:5080}/api/v1"
API_LOG="${API_LOG:?set API_LOG to the Api log file (OTP codes are read from it)}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@freshorbit.local}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:?set ADMIN_PASSWORD}"
WAIT_SECONDS="${WAIT_SECONDS:-60}"

RUN=$(( $(date +%s) % 100000 ))
SUFFIX=$(printf '%05d' "$RUN")
PINCODE="6$(printf '%05d' $(( (RUN * 7) % 100000 )))"
PHONE="+9198$(printf '%08d' $(( (RUN * 13) % 100000000 )))"
PASSED=0
FAILED=0
BODY=""
STATUS=""

pass() { PASSED=$((PASSED + 1)); printf 'PASS  %s\n' "$1"; }
fail() { FAILED=$((FAILED + 1)); printf 'FAIL  %s\n      %s\n' "$1" "$2"; summary; exit 1; }
summary() { printf '\n%d passed, %d failed\n' "$PASSED" "$FAILED"; }
uuid() { uuidgen | tr '[:upper:]' '[:lower:]'; }

# call METHOD PATH TOKEN [JSON] [extra curl args...] → sets STATUS and BODY
call() {
  local method=$1 path=$2 token=$3 data=${4:-}
  shift 4 2>/dev/null || shift $#
  local args=(-s -o /tmp/smoke_body.$$ -w '%{http_code}' -X "$method" "$API$path" -H 'Accept: application/json')
  [ -n "$token" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$data" ] && args+=(-H 'Content-Type: application/json' --data "$data")
  [ "$method" = POST ] && args+=(-H "Idempotency-Key: $(uuid)")
  STATUS=$(curl "${args[@]}" "$@")
  BODY=$(cat /tmp/smoke_body.$$ 2>/dev/null || true)
  rm -f /tmp/smoke_body.$$
}

# step NAME METHOD PATH TOKEN [JSON] [extra curl args...] → fails the run unless 2xx
step() {
  local name=$1
  shift
  call "$@"
  case "$STATUS" in
    2??) pass "$name" ;;
    *) fail "$name" "HTTP $STATUS $(printf '%s' "$BODY" | head -c 600)" ;;
  esac
}

# check NAME JQ_FILTER → passes when the filter on BODY yields true
check() {
  if printf '%s' "$BODY" | jq -e "$2" > /dev/null 2>&1; then pass "$1"; else fail "$1" "$(printf '%s' "$BODY" | head -c 600)"; fi
}

# wait_for NAME JQ_FILTER METHOD PATH TOKEN → polls (Worker handles events) until the filter holds
wait_for() {
  local name=$1 filter=$2 method=$3 path=$4 token=$5 i
  for ((i = 0; i < WAIT_SECONDS; i++)); do
    call "$method" "$path" "$token" ""
    if [ "${STATUS:0:1}" = 2 ] && printf '%s' "$BODY" | jq -e "$filter" > /dev/null 2>&1; then pass "$name"; return; fi
    sleep 1
  done
  fail "$name" "HTTP $STATUS $(printf '%s' "$BODY" | head -c 600)"
}

etag_of() { # etag_of PATH TOKEN → the ETag a GET of PATH returns
  curl -s -D - -o /dev/null "$API$1" -H "Authorization: Bearer $2" | awk 'tolower($1)=="etag:"{print $2}' | tr -d '\r'
}

IMAGE_PNG_BASE64='iVBORw0KGgoAAAANSUhEUgAAAyAAAAMgCAIAAABUEpE/AAALuklEQVR42u3WMQ0AAAgEsVeCTiQiCxMsJE2q4KZL9QAAcCgSAAAYLAAAgwUAYLAAADBYAAAGCwDAYAEAYLAAAAwWAIDBAgDAYAEAGCwAAIMFAGCwAAAwWAAABgsAwGABAGCwAAAMFgCAwQIAwGABABgsAACDBQCAwQIAMFgAAAYLAMBgAQBgsAAADBYAgMECAMBgAQAYLAAAgwUAgMECADBYAAAGCwAAgwUAYLAAAAwWAIDBAgDAYAEAGCwAAIMFAIDBAgAwWAAABgsAAIMFAGCwAAAMFgAABgsAwGABABgsAACDBQCAwQIAMFgAAAYLAACDBQBgsAAADBYAAAYLAMBgAQAYLAAAgwUAgMECADBYAAAGCwAAgwUAYLAAAAwWAAAGCwDAYAEAGCwAAAwWAIDBAgAwWAAABgsAAIMFAGCwAAAMFgAABgsAwGABABgsAAAMFgCAwQIAMFgAABgsAACDBQBgsAAADBYAAAYLAMBgAQAYLAAADBYAgMECADBYAAAYLAAAgwUAYLAAADBYAAAGCwDAYAEAGCwAAAwWAIDBAgAwWAAAGCwAAIMFAGCwAAAwWAAABgsAwGABABgsAAAMFgCAwQIAMFgAABgsAACDBQBgsAAAMFgAAAYLAMBgAQBgsAAADBYAgMECADBYAAAYLAAAgwUAYLAAADBYAAAGCwDAYAEAYLAAAAwWAIDBAgDAYAEAGCwAAIMFAGCwAAAwWAAABgsAwGABAGCwAAAMFgCAwQIAwGABABgsAACDBQCAwQIAMFgAAAYLAMBgAQBgsAAADBYAgMECAMBgAQAYLAAAgwUAgMECADBYAAAGCwDAYKkAAGCwAAAMFgCAwQIAwGABABgsAACDBQCAwQIAMFgAAAYLAACDBQBgsAAADBYAgMECAMBgAQAYLAAAgwUAgMECADBYAAAGCwAAgwUAYLAAAAwWAAAGCwDAYAEAGCwAAIMFAIDBAgAwWAAABgsAAIMFAGCwAAAMFgAABgsAwGABABgsAAAMFgCAwQIAMFgAAAYLAACDBQBgsAAADBYAAAYLAMBgAQAYLAAADBYAgMECADBYAAAGSwUAAIMFAGCwAAAMFgAABgsAwGABABgsAAAMFgCAwQIAMFgAABgsAACDBQBgsAAADBYAAAYLAMBgAQAYLAAADBYAgMECADBYAAAYLAAAgwUAYLAAADBYAAAGCwDAYAEAGCwAAAwWAIDBAgAwWAAAGCwAAIMFAGCwAAAwWAAABgsAwGABAGCwAAAMFgCAwQIAMFgAABgsAACDBQBgsAAAMFgAAAYLAMBgAQBgsAAADBYAgMECADBYEgAAGCwAAIMFAGCwAAAwWAAABgsAwGABAGCwAAAMFgCAwQIAwGABABgsAACDBQBgsAAAMFgAAAYLAMBgAQBgsAAADBYAgMECAMBgAQAYLAAAgwUAgMECADBYAAAGCwDAYAEAYLAAAAwWAIDBAgDAYAEAGCwAAIMFAIDBAgAwWAAABgsAAIMFAGCwAAAMFgCAwQIAwGABABgsAACDBQCAwQIAMFgAAAYLAACDBQBgsAAADBYAAAYLAMBgAQAYLAAAgwUAgMECADBYAAAGCwAAgwUAYLAAAAwWAAAGCwDAYAEAGCwAAIMFAIDBAgAwWAAABgsAAIMFAGCwAAAMFgAABgsAwGABABgsAAAMFgCAwQIAMFgAAAYLAACDBQBgsAAADBYAAAYLAMBgAQAYLAAADBYAgMECADBYAAAYLAAAgwUAYLAAAAwWAAAGCwDAYAEAGCwAAAwWAIDBAgAwWAAAGCwAAIMFAGCwAAAwWAAABgsAwGABABgsAAAMFgCAwQIAMFgAABgsAACDBQBgsAAAMFgAAAYLAMBgAQAYLAAADBYAgMECADBYAAAYLAAAgwUAYLAAADBYAAAGCwDAYAEAYLAAAAwWAIDBAgAwWAAAGCwAAIMFAGCwAAAwWAAABgsAwGABAGCwAAAMFgCAwQIAwGABABgsAACDBQBgsAAAMFgAAAYLAMBgAQBgsAAADBYAgMECAMBgAQAYLAAAgwUAgMECADBYAAAGCwDAYAEAYLAAAAwWAIDBAgDAYAEAGCwAAIMFAIDBAgAwWAAABgsAwGCpAABgsAAADBYAgMECAMBgAQAYLAAAgwUAgMECADBYAAAGCwAAgwUAYLAAAAwWAIDBAgDAYAEAGCwAAIMFAIDBAgAwWAAABgsAAIMFAGCwAAAMFgAABgsAwGABABgsAACDBQCAwQIAMFgAAAYLAACDBQBgsAAADBYAAAYLAMBgAQAYLAAADBYAgMECADBYAAAGCwAAgwUAYLAAAAwWAAAGCwDAYAEAGCwAAAwWAIDBAgAwWAAABksFAACDBQBgsAAADBYAAAYLAMBgAQAYLAAADBYAgMECADBYAAAYLAAAgwUAYLAAAAwWAAAGCwDAYAEAGCwAAAwWAIDBAgAwWAAAGCwAAIMFAGCwAAAwWAAABgsAwGABABgsAAAMFgCAwQIAMFgAABgsAACDBQBgsAAAMFgAAAYLAMBgAQBgsAAADBYAgMECADBYAAAYLAAAgwUAYLAAADBYAAAGCwDAYAEAYLAAAAwWAIDBAgAwWBIAABgsAACDBQBgsAAAMFgAAAYLAMBgAQBgsAAADBYAgMECAMBgAQAYLAAAgwUAYLAAADBYAAAGCwDAYAEAYLAAAAwWAIDBAgDAYAEAGCwAAIMFAIDBAgAwWAAABgsAwGABAGCwAAAMFgCAwQIAwGABABgsAACDBQCAwQIAMFgAAAYLAACDBQBgsAAADBYAgMECAMBgAQAYLAAAgwUAgMECADBYAAAGCwAAgwUAYLAAAAwWAAAGCwDAYAEAGCwAAIMFAIDBAgAwWAAABgsAAIMFAGCwAAAMFgAABgsAwGABABgsAACDBQCAwQIAMFgAAAYLAACDBQBgsAAADBYAAAYLAMBgAQAYLAAADBYAgMECADBYAAAGCwAAgwUAYLAAAAwWAAAGCwDAYAEAGCwAAAwWAIDBAgAwWAAAGCwAAIMFAGCwAAAMFgAABgsAwGABABgsAAAMFgCAwQIAMFgAABgsAACDBQBgsAAAMFgAAAYLAMBgAQAYLAAADBYAgMECADBYAAAYLAAAgwUAYLAAADBYAAAGCwDAYAEAGCwAAAwWAIDBAgAwWAAAGCwAAIMFAGCwAAAwWAAABgsAwGABAGCwAAAMFgCAwQIAMFgAABgsAACDBQBgsAAAMFgAAAYLAMBgAQBgsAAADBYAgMECAMBgAQAYLAAAgwUAYLAAADBYAAAGCwDAYAEAYLAAAAwWAIDBAgDAYAEAGCwAAIMFAIDBAgAwWAAABgsAwGABAGCwAAAMFgCAwQIAwGABABgsAACDBQCAwQIAMFgAAAYLAMBgqQAAYLAAAAwWAIDBAgDAYAEAGCwAAIMFAIDBAgAwWAAABgsAAIMFAGCwAAAMFgCAwQIAwGABABgsAACDBQCAwQIAMFgAAAYLAACDBQBgsAAADBYAAAYLAMBgAQAYLAAAgwUAgMECADBYAAAGCwAAgwUAYLAAAAwWAAAGCwDAYAEAGCwAAAwWAIDBAgAwWAAABgsAAIMFAGCwAAAMFgAABgsAwGABABgsAAAMFgCAwQIAMFgAAAZLBQAAgwUAYLAAAAwWAAAGCwDAYAEAGCwAAAwWAIDBAgAwWAAAGCwAAIMFAGCwAAAMFgAABgsAwGABABgsAAAMFgCAwQIAMFgAABgsAACDBQBgsAAAMFgAAAYLAMBgAQAYLAAADBYAgMECADBYAAAYLAAAgwUAYLAAADBYAAAGCwDAYAEAYLAAAAwWAIDBAgAwWAAAGCwAAIMFAGCwAAAwWAAABgsAwGABAGCwAAAMFgCAwQIAMFgSAAAYLAAAgwUAYLAAADBYAAAGCwDAYAEAYLAAAAwWAIDBAgDAYAEAGCwAAIMFAGCwAAAwWAAABgsAwGABAGCwAAAMFgCAwQIAwGABABgsAACDBQCAwQIAMFgAAF8su73ARwVHmOkAAAAASUVORK5CYII='

command -v jq > /dev/null || { echo "jq is required"; exit 2; }
echo "Smoke run $SUFFIX against $API (pincode $PINCODE, phone $PHONE)"

# --- Staff -------------------------------------------------------------------------------------------------------
step "staff login" POST /admin/auth/login "" "$(jq -nc --arg e "$ADMIN_EMAIL" --arg p "$ADMIN_PASSWORD" '{email:$e,password:$p}')"
STAFF=$(jq -r .accessToken <<< "$BODY")

# Order and invoice numbers need the business profile (its time zone sets the financial year).
PROFILE='{"legalName":"Fresh Orbit Private Limited","tradeName":"Fresh Orbit","gstin":"33AAACF1234A1ZM","fssaiLicenseNo":"12345678901234","address":"1 Anna Salai, Chennai 600002","supportPhone":"+914412345678","supportEmail":"support@freshorbit.local","timeZone":"Asia/Kolkata"}'
PROFILE_ETAG=$(etag_of /admin/settings/business-profile "$STAFF")
if [ -n "$PROFILE_ETAG" ]; then
  step "business profile (update)" PUT /admin/settings/business-profile "$STAFF" "$PROFILE" -H "If-Match: $PROFILE_ETAG"
else
  step "business profile (create)" PUT /admin/settings/business-profile "$STAFF" "$PROFILE" -H 'If-None-Match: *'
fi

step "create city" POST /admin/cities "$STAFF" "$(jq -nc --arg c "SMK$SUFFIX" '{code:$c,name:("Smoke " + $c),stateCode:"TN",timeZone:"Asia/Kolkata"}')"
CITY=$(jq -r .id <<< "$BODY")

step "create fulfillment location" POST /admin/fulfillment-locations "$STAFF" "$(jq -nc --arg city "$CITY" --arg c "SMK-FL-$SUFFIX" --arg pin "$PINCODE" \
  '{cityId:$city,code:$c,name:("Store " + $c),addressLine:"1 Harbour Road",pincode:$pin,phone:"+914412345678",latitude:13.08,longitude:80.27,hasColdStorage:true,fssaiLicenseNo:"12345678901234"}')"
LOCATION=$(jq -r .id <<< "$BODY")

step "create service area" POST /admin/service-areas "$STAFF" "$(jq -nc --arg city "$CITY" --arg loc "$LOCATION" \
  '{cityId:$city,fulfillmentLocationId:$loc,name:"Harbour",deliveryFeePaise:3000,freeDeliveryAbovePaise:null,minOrderPaise:10000}')"
AREA=$(jq -r .id <<< "$BODY")

step "add pincode" POST "/admin/service-areas/$AREA/pincodes" "$STAFF" "$(jq -nc --arg pin "$PINCODE" '{pincodes:[$pin]}')" \
  -H "If-Match: $(etag_of "/admin/service-areas/$AREA" "$STAFF")"

HOURS=$(jq -nc '{days:["SUNDAY","MONDAY","TUESDAY","WEDNESDAY","THURSDAY","FRIDAY","SATURDAY"] | map({dayOfWeek:., opensAt:"06:00", closesAt:"23:00"})}')
step "set operating hours" PUT "/admin/fulfillment-locations/$LOCATION/operating-hours" "$STAFF" "$HOURS" \
  -H "If-Match: $(etag_of "/admin/fulfillment-locations/$LOCATION" "$STAFF")"

for day in 1 2 3 4 5 6 7; do
  call POST /admin/delivery/slot-templates "$STAFF" "$(jq -nc --arg loc "$LOCATION" --argjson d "$day" \
    '{fulfillmentLocationId:$loc,dayOfWeek:$d,startTime:"07:00",endTime:"22:00",capacity:50,cutoffMinutes:30}')"
  [ "${STATUS:0:1}" = 2 ] || fail "create slot template (day $day)" "HTTP $STATUS $BODY"
done
pass "create slot templates (7 days)"

step "create category" POST /admin/categories "$STAFF" "$(jq -nc --arg s "smoke-fish-$SUFFIX" '{parentId:null,slug:$s,name:("Smoke fish " + $s),description:null,sortOrder:1}')"
CATEGORY=$(jq -r .id <<< "$BODY")

step "create product" POST /admin/products "$STAFF" "$(jq -nc --arg cat "$CATEGORY" --arg s "smoke-seer-$SUFFIX" \
  '{categoryId:$cat,slug:$s,name:("Seer fish " + $s),localNames:["Vanjaram"],speciesScientificName:"Scomberomorus commerson",description:"Smoke test product",sourceType:"WILD",sourceRegion:"Kasimedu",storageInstructions:"Keep at 0-4 °C",handlingNotes:null,hsnCode:"0302",taxCategoryCode:"SEAFOOD_FRESH_CHILLED",featuredRank:null}')"
PRODUCT=$(jq -r .id <<< "$BODY")
PRODUCT_SLUG="smoke-seer-$SUFFIX"

step "create variant with preparations" POST "/admin/products/$PRODUCT/variants" "$STAFF" "$(jq -nc --arg sku "SMK-SEER-$SUFFIX" \
  '{sku:$sku,name:"Whole fish",sellUnit:"KG",packSizeGrams:null,avgPieceGrams:null,minOrder:500,maxOrder:5000,orderStep:250,isActive:true,preparations:[{code:"WHOLE",yieldBp:10000,isDefault:true},{code:"CURRY_CUT",yieldBp:8000,isDefault:false}]}')" \
  -H "If-Match: $(etag_of "/admin/products/$PRODUCT" "$STAFF")"
VARIANT=$(jq -r '(.variants // [.])[0].id // .id' <<< "$BODY")

# An 800×800 PNG (publishing needs one image with alt text; images must be at least 800 px).
IMAGE_FILE=$(mktemp -t smoke-image).png
printf '%s' "$IMAGE_PNG_BASE64" | { base64 -d 2>/dev/null || base64 -D; } > "$IMAGE_FILE"
step "upload product image" POST "/admin/products/$PRODUCT/images" "$STAFF" "" -F "file=@$IMAGE_FILE;type=image/png" -F "altText=Whole seer fish" \
  -H "If-Match: $(etag_of "/admin/products/$PRODUCT" "$STAFF")"
rm -f "$IMAGE_FILE"

step "publish product" POST "/admin/products/$PRODUCT/publish" "$STAFF" "" -H "If-Match: $(etag_of "/admin/products/$PRODUCT" "$STAFF")"

for tax in SEAFOOD_FRESH_CHILLED:500 DELIVERY_SERVICE:1800; do
  call POST /admin/pricing/tax-rules "$STAFF" "$(jq -nc --arg c "${tax%%:*}" --argjson r "${tax##*:}" '{taxCategoryCode:$c,rateBp:$r,validFrom:null,replaceFuture:true}')"
  case "$STATUS" in 2??|409) ;; *) fail "tax rule ${tax%%:*}" "HTTP $STATUS $BODY" ;; esac
done
pass "tax rules (SEAFOOD_FRESH_CHILLED 5%, DELIVERY_SERVICE 18%)"
step "set price" PUT "/admin/pricing/locations/$LOCATION/variants/$VARIANT/price" "$STAFF" \
  "$(jq -nc '{unitPricePaise:90000,compareAtPaise:null,validFrom:null,reason:"smoke",replaceFuture:true}')" -H 'If-None-Match: *'
step "set WHOLE charge (free)" PUT "/admin/pricing/locations/$LOCATION/variants/$VARIANT/preparations/WHOLE/charge" "$STAFF" \
  '{"chargePaise":0,"chargeBasis":"FLAT","validFrom":null,"replaceFuture":true}' -H 'If-None-Match: *'
step "set CURRY_CUT charge" PUT "/admin/pricing/locations/$LOCATION/variants/$VARIANT/preparations/CURRY_CUT/charge" "$STAFF" \
  "$(jq -nc '{chargePaise:4000,chargeBasis:"PER_KG",validFrom:null,replaceFuture:true}')" -H 'If-None-Match: *'


step "create stock item" POST /admin/inventory/stock-items "$STAFF" "$(jq -nc --arg loc "$LOCATION" --arg v "$VARIANT" '{fulfillmentLocationId:$loc,variantId:$v,lowStockThreshold:1000}')"
BEST_BEFORE=$(date -u -v+2d +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -d '+2 days' +%Y-%m-%dT%H:%M:%SZ)
step "receive stock (20 kg)" POST /admin/inventory/receipts "$STAFF" "$(jq -nc --arg loc "$LOCATION" --arg v "$VARIANT" --arg b "SMK-B-$SUFFIX" --arg bb "$BEST_BEFORE" \
  '{fulfillmentLocationId:$loc,variantId:$v,batchCode:$b,supplierName:"Kasimedu landing",sourceLandingCenter:"Kasimedu",caughtOrHarvestedOn:null,receivedQuantity:{value:20000,unit:"GRAM"},totalCostPaise:1200000,bestBeforeAt:$bb,receiptTemperatureC:2.5}')"

step "activate fulfillment location" PUT "/admin/fulfillment-locations/$LOCATION" "$STAFF" "$(jq -nc --arg pin "$PINCODE" \
  '{name:"Smoke store",addressLine:"1 Harbour Road",pincode:$pin,phone:"+914412345678",latitude:13.08,longitude:80.27,hasColdStorage:true,fssaiLicenseNo:"12345678901234",status:"ACTIVE"}')" \
  -H "If-Match: $(etag_of "/admin/fulfillment-locations/$LOCATION" "$STAFF")"

step "city readiness" GET "/admin/cities/$CITY/readiness" "$STAFF" ""
printf '      %s\n' "$(jq -c . <<< "$BODY" | head -c 400)"
step "activate city" POST "/admin/cities/$CITY/activate" "$STAFF" "" -H "If-Match: $(etag_of "/admin/cities/$CITY" "$STAFF")"
wait_for "delivery slots generated" '[.. | objects | select(has("startAt"))] | length > 0' GET "/delivery-slots?pincode=$PINCODE" ""

# --- Customer ------------------------------------------------------------------------------------------------------
step "OTP request" POST /auth/otp/request "" "$(jq -nc --arg p "$PHONE" '{phone:$p,purpose:"LOGIN"}')"
CHALLENGE=$(jq -r .challengeId <<< "$BODY")
OTP=""
for ((i = 0; i < 20; i++)); do
  OTP=$(grep -F "${PHONE}" "$API_LOG" | grep -oE '\b[0-9]{6}\b' | tail -1 || true)
  [ -n "$OTP" ] && break
  sleep 0.5
done
[ -n "$OTP" ] || fail "OTP code in Api log" "no DEV SMS line for $PHONE in $API_LOG"
pass "OTP code in Api log"
step "OTP verify" POST /auth/otp/verify "" "$(jq -nc --arg c "$CHALLENGE" --arg p "$PHONE" --arg o "$OTP" '{challengeId:$c,phone:$p,code:$o,deviceLabel:"smoke",guestCartToken:null}')"
CUSTOMER=$(jq -r .accessToken <<< "$BODY")

step "serviceability" GET "/serviceability?pincode=$PINCODE" "" ""
check "pincode is serviceable" '.serviceable == true'
SLOT=$(jq -r .nextSlot.id <<< "$BODY")

step "product list" GET "/products" "" "" -H "X-Pincode: $PINCODE"
check "product list shows the price" ".items[] | select(.slug == \"$PRODUCT_SLUG\") | .fromPrice.unitPricePaise == 90000"
IMAGE_URL=$(jq -r --arg slug "$PRODUCT_SLUG" '.items[] | select(.slug == $slug) | .imageUrl' <<< "$BODY")
MEDIA_STATUS=$(curl -s -o /dev/null -w '%{http_code}' "${API%/api/v1}$IMAGE_URL")
[ "$MEDIA_STATUS" = 200 ] && pass "product image served from /media" || fail "product image served from /media" "HTTP $MEDIA_STATUS for $IMAGE_URL"

step "add address" POST /me/addresses "$CUSTOMER" "$(jq -nc --arg pin "$PINCODE" --arg ph "$PHONE" \
  '{label:"HOME",recipientName:"Smoke Customer",phone:$ph,line1:"12 Beach Road",line2:null,landmark:null,locality:"Harbour",cityName:"Chennai",stateCode:"TN",pincode:$pin,latitude:null,longitude:null,isDefault:true}')"
ADDRESS=$(jq -r .addressId <<< "$BODY")
check "address is serviceable" '.isServiceable == true'

step "set cart location" PUT /cart/location "$CUSTOMER" "$(jq -nc --arg pin "$PINCODE" '{pincode:$pin}')"
step "cart add (1 kg curry cut)" POST /cart/items "$CUSTOMER" "$(jq -nc --arg v "$VARIANT" '{variantId:$v,preparationCode:"CURRY_CUT",quantity:{value:1000,unit:"GRAM"}}')"

CHECKOUT=$(jq -nc --arg a "$ADDRESS" --arg s "$SLOT" '{addressId:$a,deliverySlotId:$s,paymentMethod:"COD",substitutionPreference:null,customerNotes:"smoke",expectedTotalPaise:null}')
step "checkout preview" POST /checkout/preview "$CUSTOMER" "$CHECKOUT"
TOTAL=$(jq -r '.. | objects | select(has("estimatedTotalPaise")) | .estimatedTotalPaise' <<< "$BODY" | head -1)
[ -n "$TOTAL" ] && [ "$TOTAL" != null ] && pass "preview total $TOTAL paise" || fail "preview total" "$(head -c 600 <<< "$BODY")"

step "place COD order" POST /orders "$CUSTOMER" "$(jq -c --argjson t "$TOTAL" '.expectedTotalPaise = $t' <<< "$CHECKOUT")"
ORDER=$(jq -r '.order.id // .id' <<< "$BODY")
printf '      order %s\n' "$ORDER"

# --- Fulfilment ----------------------------------------------------------------------------------------------------
wait_for "order confirmed" '(.order // .).status == "CONFIRMED"' GET "/admin/orders/$ORDER" "$STAFF"
ITEMS=$(jq -c '[(.order // .).items[] | {orderItemId:.id, packedQuantity:{value:(.quantity.value // .orderedQuantity.value // 1000), unit:(.quantity.unit // .orderedQuantity.unit // "GRAM")}}]' <<< "$BODY")
step "start preparing" POST "/admin/orders/$ORDER/start-preparing" "$STAFF" "" -H "If-Match: $(etag_of "/admin/orders/$ORDER" "$STAFF")"
step "pack" POST "/admin/orders/$ORDER/pack" "$STAFF" "$(jq -nc --argjson l "$ITEMS" '{lines:$l,coldBoxUsed:true}')" -H "If-Match: $(etag_of "/admin/orders/$ORDER" "$STAFF")"

wait_for "shipment ready" "[.items[] | select(.orderId == \"$ORDER\")] | length > 0" GET "/admin/delivery/shipments?fulfillmentLocationId=$LOCATION" "$STAFF"
SHIPMENT=$(jq -r --arg o "$ORDER" '.items[] | select(.orderId == $o) | .id' <<< "$BODY")
step "create rider" POST /admin/delivery/riders "$STAFF" "$(jq -nc --arg loc "$LOCATION" --arg ph "+9197$(printf '%08d' "$RUN")" '{fulfillmentLocationId:$loc,fullName:"Smoke Rider",phone:$ph}')"
RIDER=$(jq -r .id <<< "$BODY")
step "assign rider" POST "/admin/delivery/shipments/$SHIPMENT/assign" "$STAFF" "$(jq -nc --arg r "$RIDER" '{riderId:$r}')" -H "If-Match: $(etag_of "/admin/delivery/shipments/$SHIPMENT" "$STAFF")"
step "dispatch" POST "/admin/delivery/shipments/$SHIPMENT/dispatch" "$STAFF" '{"dispatchTemperatureC":2.0}' -H "If-Match: $(etag_of "/admin/delivery/shipments/$SHIPMENT" "$STAFF")"

wait_for "customer sees delivery code" '.deliveryCode | type == "string"' GET "/orders/$ORDER/tracking" "$CUSTOMER"
CODE=$(jq -r .deliveryCode <<< "$BODY")
DUE=$(jq -r .amountDuePaise <<< "$BODY")
step "deliver with code" POST "/admin/delivery/shipments/$SHIPMENT/deliver" "$STAFF" "$(jq -nc --arg c "$CODE" --argjson amt "$DUE" '{deliveryCode:$c,cashCollectedPaise:$amt,cashMode:"CASH",upiReference:null}')" \
  -H "If-Match: $(etag_of "/admin/delivery/shipments/$SHIPMENT" "$STAFF")"

# --- Outcomes ------------------------------------------------------------------------------------------------------
wait_for "order delivered" '.status == "DELIVERED"' GET "/orders/$ORDER" "$CUSTOMER"
wait_for "invoice issued" '(.invoiceNumber // .number // "") | length > 0' GET "/orders/$ORDER/invoice" "$CUSTOMER"
wait_for "payment COLLECTED" '[.payments[] | select(.status == "COLLECTED")] | length > 0' GET "/admin/orders/$ORDER/payments" "$STAFF"
wait_for "stock consumed (SALE movement)" '[(.items // .)[] | select(.kind == "SALE")] | length > 0' GET "/admin/inventory/movements?fulfillmentLocationId=$LOCATION&variantId=$VARIANT" "$STAFF"
wait_for "notifications queued" '[(.items // .)[]] | length > 0' GET "/me/notifications" "$CUSTOMER"

summary
[ "$FAILED" -eq 0 ]
