import { type SelectOption } from "@fresh/ui";
import { type AdminCategory } from "../../api/types/catalog";

export interface FlatCategory {
  category: AdminCategory;
  depth: number;
  /** "Fish › Sea fish". */
  path: string;
}

/** Depth-first list of the category tree, keeping the server's order. */
export function flattenCategories(
  tree: readonly AdminCategory[],
  depth = 0,
  prefix = "",
): FlatCategory[] {
  return tree.flatMap((c) => {
    const path = prefix ? `${prefix} › ${c.name}` : c.name;
    return [{ category: c, depth, path }, ...flattenCategories(c.children ?? [], depth + 1, path)];
  });
}

export function categoryOptions(tree: readonly AdminCategory[] | undefined): SelectOption[] {
  return flattenCategories(tree ?? []).map(({ category, path }) => ({
    value: category.id,
    label: category.isActive ? path : `${path} (inactive)`,
  }));
}
