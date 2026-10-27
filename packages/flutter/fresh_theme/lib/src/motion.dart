// The one entrance motion of the app (a short rise and fade), off when the platform asks for less motion.

import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:fresh_theme/fresh_theme.dart';

/// False when the system or the user asked for reduced motion.
bool motionAllowed(BuildContext context) => !(MediaQuery.maybeDisableAnimationsOf(context) ?? false);

class EntranceMotion extends StatefulWidget {
  const EntranceMotion({
    super.key,
    required this.child,
    this.delay = Duration.zero,
    this.duration = const Duration(milliseconds: 420),
    this.rise = 12,
    this.fromTurns = 0,
  });

  final Widget child;
  final Duration delay;
  final Duration duration;

  /// Logical pixels the child rises while it fades in.
  final double rise;

  /// Extra rotation at the start, in turns (polaroids settle from a slightly larger tilt).
  final double fromTurns;

  @override
  State<EntranceMotion> createState() => _EntranceMotionState();
}

class _EntranceMotionState extends State<EntranceMotion> with SingleTickerProviderStateMixin {
  AnimationController? _controller;
  CurvedAnimation? _settle;
  CurvedAnimation? _fade;
  bool _started = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_started) {
      // Reduced motion switched on mid-way: jump to the final state.
      if (!motionAllowed(context)) _controller?.value = 1;
      return;
    }
    _started = true;
    if (!motionAllowed(context)) return;
    final total = widget.delay + widget.duration;
    final start = total == Duration.zero ? 0.0 : widget.delay.inMicroseconds / total.inMicroseconds;
    // The delay is part of the controller's run (an Interval), so no timer outlives the widget.
    final controller = AnimationController(vsync: this, duration: total);
    _controller = controller;
    _settle = CurvedAnimation(
      parent: controller,
      curve: Interval(start, 1, curve: Curves.easeOutBack),
    );
    _fade = CurvedAnimation(
      parent: controller,
      curve: Interval(start, 1, curve: Curves.easeOut),
    );
    controller.forward();
  }

  @override
  void dispose() {
    _settle?.dispose();
    _fade?.dispose();
    _controller?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final controller = _controller;
    final settle = _settle;
    final fade = _fade;
    // Reduced motion: final state, no ticker.
    if (controller == null || settle == null || fade == null) return widget.child;
    return AnimatedBuilder(
      animation: controller,
      child: widget.child,
      builder: (context, child) => Opacity(
        opacity: fade.value.clamp(0.0, 1.0),
        child: Transform.translate(
          offset: Offset(0, widget.rise * (1 - settle.value)),
          child: Transform.rotate(angle: widget.fromTurns * 2 * math.pi * (1 - settle.value), child: child),
        ),
      ),
    );
  }
}
