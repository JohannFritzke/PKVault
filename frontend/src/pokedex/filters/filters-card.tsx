import type React from "react";
import { withErrorCatcher } from '../../error/with-error-catcher';
import type { WithControlsIconsExtraProps } from '../../ui/interaction/controls/icons/with-controls-icons';
import { UIPokedexFilterGroup, UIPokedexFilters } from '../../ui/pokedex/filters/ui-pokedex-filters';
import { FilterAlpha } from './components/filter-alpha';
import { FilterCaught } from "./components/filter-caught";
import { FilterFromGames } from "./components/filter-from-games";
import { FilterGeneration } from "./components/filter-generation";
import { FilterOwned } from './components/filter-owned';
import { FilterOwnedShiny } from './components/filter-owned-shiny';
import { FilterSeen } from "./components/filter-seen";
import { FilterSpecies } from "./components/filter-species";
import { FilterTypes } from "./components/filter-types";
import { FilterViews } from './components/filter-views';
import { useTranslate } from '../../translate/i18n';

export const FiltersCard: React.FC<WithControlsIconsExtraProps> = withErrorCatcher('default', (extraProps) => {
  const { t } = useTranslate();

  return <UIPokedexFilters
    species={<FilterSpecies />}
    views={<FilterViews />}
    {...extraProps}
  >
    <UIPokedexFilterGroup label={t('dex.filters.status')}>
      <FilterSeen />
      <FilterCaught />
      <FilterOwned />
      <FilterOwnedShiny />
      <FilterAlpha />
    </UIPokedexFilterGroup>

    <FilterGeneration />
    <FilterTypes />
    <FilterFromGames />
  </UIPokedexFilters>;
});
