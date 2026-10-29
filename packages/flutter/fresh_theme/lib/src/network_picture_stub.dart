// Web (no file system): the browser's own HTTP cache keeps the pictures (network_picture.dart).

import 'package:flutter/painting.dart';

ImageProvider<Object> cachedNetworkImage(String url) => NetworkImage(url);
