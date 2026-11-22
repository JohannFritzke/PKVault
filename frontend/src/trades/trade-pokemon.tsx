import type React from 'react';
import { Gender, type TradePokemonSummaryDTO } from '../data/sdk/model';
import { useStaticData } from '../hooks/use-static-data';
import { SpeciesImg } from '../img/species-img';
import { useTranslate } from '../translate/i18n';
import { UITradePokemon } from '../ui/trades/ui-trade-card';
import { getFormatContext } from './trade-util';

type TradePokemonProps = {
    pokemon?: TradePokemonSummaryDTO;
    emptyLabel: string;
};

export const TradePokemon: React.FC<TradePokemonProps> = ({ pokemon, emptyLabel }) => {
    const { t } = useTranslate();
    const staticData = useStaticData();

    if (!pokemon) {
        return <UITradePokemon emptyLabel={emptyLabel} />;
    }

    const context = getFormatContext(pokemon.format);
    const forms = staticData.species[ pokemon.species ]?.forms[ context ];
    const speciesName = (forms?.[ pokemon.form ] ?? forms?.[ 0 ])?.name ?? `#${pokemon.species}`;
    const name = pokemon.nickname && pokemon.nickname.toUpperCase() !== speciesName.toUpperCase()
        ? `${pokemon.nickname} (${speciesName})`
        : speciesName;

    return <UITradePokemon
        img={<SpeciesImg
            species={pokemon.species}
            context={context}
            form={pokemon.form}
            isFemale={pokemon.gender === Gender.Female}
            isShiny={pokemon.shiny}
        />}
        name={`${pokemon.shiny ? '★ ' : ''}${name}`}
        meta={[
            t('trades.pokemon.level', { level: pokemon.level }),
            pokemon.otName && t('trades.pokemon.ot', { name: pokemon.otName }),
        ].filter(Boolean).join(' · ')}
    />;
};
