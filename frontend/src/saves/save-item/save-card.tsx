import type React from 'react';
import { EntityContext } from '../../data/sdk/model';
import { useSaveInfosGetAll } from '../../data/sdk/save-infos/save-infos.gen';
import { getEntityContextGenerationName } from '../../data/util/get-entity-context-generation-name';
import { useStaticData } from '../../hooks/use-static-data';
import { getGameInfos } from '../../pokedex/details/util/get-game-infos';
import { UISaveCard, type UISaveCardProps } from '../../ui/saves/save-card/ui-save-card';
import { SaveItemEdit } from './save-item-edit';

type SaveCardProps = Pick<UISaveCardProps, 'onSelect' | 'actions'> & {
    saveId: number;
};

export const SaveCard: React.FC<SaveCardProps> = ({ saveId, onSelect, actions }) => {
    const staticData = useStaticData();

    const saveInfosQuery = useSaveInfosGetAll();

    const loading = saveInfosQuery.isPending && saveInfosQuery.isEnabled;

    const save = saveInfosQuery.data?.data[ saveId ];
    if (!save) {
        return null;
    }

    const versionObj = staticData.versions[ save.displayedVersion ];

    const hasDuplicates = save.duplicates.length > 0;
    const hasEdit = (versionObj && !versionObj.isGameVersion) || hasDuplicates;

    return <UISaveCard
        id={save.id.toString()}
        generation={getEntityContextGenerationName(save.context ?? EntityContext.None, true)}
        label={versionObj?.name}
        imgSrc={getGameInfos(save.displayedVersion).img}
        path={save.path}
        ot={save.trainerName}
        otGender={save.trainerGender}
        tid={save.tid}
        language={staticData.languages[ save.language ]}
        caughtCount={save.dexCaughtCount}
        ownedCount={save.ownedCount}
        shinyCount={save.shinyCount}
        playTime={save.playTime}
        loading={loading}
        hasDuplicates={hasDuplicates}
        onSelect={onSelect}
        editDropdown={hasEdit && <SaveItemEdit saveId={save.id} />}
        actions={actions}
    />;
};
