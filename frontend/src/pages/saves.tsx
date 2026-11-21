import { ActionIcon, EmptyState, Tooltip } from '@mantine/core';
import { DownloadIcon, FolderIcon, FolderSearchIcon, PackageOpenIcon, TrashIcon, TriangleAlertIcon } from 'lucide-react';
import type React from "react";
import { HistoryContext } from '../context/history-context';
import { getApiFullUrl } from '../data/mutator/custom-instance';
import { DesktopMessageType } from '../data/sdk/model';
import { getSaveInfosDownloadUrl, useSaveInfosDelete, useSaveInfosGetAll } from '../data/sdk/save-infos/save-infos.gen';
import { useSettingsGet } from '../data/sdk/settings/settings.gen';
import { withErrorCatcher } from '../error/with-error-catcher';
import { useStaticData } from '../hooks/use-static-data';
import { Route } from '../routes/saves';
import { SaveCard } from '../saves/save-item/save-card';
import { SavesUploadButton } from '../saves/saves-upload-popover/saves-upload-button';
import { useDesktopMessage } from '../settings/globs-input/hooks/use-desktop-message';
import { useTranslate } from '../translate/i18n';
import { UIButton } from '../ui/form/button/ui-button';
import { UIConfirmPopover } from '../ui/popover/ui-confirm-popover';
import { UISavesContent } from '../ui/saves/ui-saves-content';
import { UISavesHeader, UISavesScroll, UISavesSection } from '../ui/saves/ui-saves-layout';
import { filterIsDefined } from '../util/filter-is-defined';

export const SavesPage: React.FC = withErrorCatcher('default', () => {
  const { t } = useTranslate();

  const navigate = Route.useNavigate();

  const storageHistoryValue = HistoryContext.useValue()[ '/storage' ];

  const desktopMessage = useDesktopMessage();

  const staticData = useStaticData();
  const saveInfosQuery = useSaveInfosGetAll();
  const saveDeleteMutation = useSaveInfosDelete();

  const settingsQuery = useSettingsGet();
  const settings = settingsQuery.data?.data;

  const isLoading = saveInfosQuery.isPending && saveInfosQuery.isEnabled;

  const generations = [ ...new Set(Object.values(staticData.versions).map(version => version.generation)) ].sort();

  const saveInfos = Object.values(saveInfosQuery.data?.data ?? {})
    .filter(filterIsDefined)
    .sort((a, b) => {
      return a.lastWriteTime > b.lastWriteTime ? -1 : 1;
    });

  const addPathLabel = settings?.canUploadSaves
    ? t('saves.action.add-path')
    : t('settings.form.saves.add-path');

  return <UISavesContent>
    <UISavesScroll>
      <UISavesHeader
        title={t('header.saves')}
        description={t('saves.subtitle')}
        actions={<>
          <UIButton
            name='add-game-path'
            controlLabel={addPathLabel}
            onClick={() => navigate({
              to: '/settings'
            })}
            leftSection={<FolderSearchIcon />}
            variant='default'
          >
            {addPathLabel}
          </UIButton>

          {settings?.canUploadSaves && <SavesUploadButton
            disabledLabel={t('action.not-possible')}
          />}
        </>}
      />

      {!isLoading && saveInfos.length === 0 && <EmptyState
        size='sm'
        py='md'
        icon={<PackageOpenIcon />}
        title={t('settings.form.saves.empty')}
      />}

      {generations.map(generation => {
        const saves = saveInfos
          .filter(save => save.generation === generation)
          .sort((s1, s2) => s1.displayedVersion - s2.displayedVersion);
        if (saves.length === 0) {
          return null;
        }

        const maxSpecies = Math.max(...saves.map(save => staticData.versions[ save.displayedVersion ]?.maxSpeciesId ?? 0));

        const regions = staticData.generations[ generation ]?.regions ?? [];

        return <UISavesSection
          key={generation}
          title={t('saves.title', { generation })}
          regions={regions}
          count={t('saves.title.species', { maxSpecies })}
        >
          {saves.map(save => <SaveCard
            key={save.id}
            saveId={save.id}
            onSelect={() => navigate({
              to: '/storage',
              search: {
                storages: [
                  storageHistoryValue?.search.storages?.[ 0 ] ?? { saveId: null },
                  {
                    saveId: save.id,
                  }
                ],
              },
            })}
            actions={desktopMessage.openFile
              ? <Tooltip label={t('saves.action.open')}>
                <ActionIcon
                  variant='subtle'
                  color='gray'
                  aria-label={t('saves.action.open')}
                  onClick={() => desktopMessage.openFile!({
                    type: DesktopMessageType.OPEN_FOLDER,
                    directoryOnly: false,
                    basePath: save.path
                  })}
                >
                  <FolderIcon />
                </ActionIcon>
              </Tooltip>
              : <>
                <Tooltip label={t('saves.action.download')}>
                  <ActionIcon
                    variant='subtle'
                    color='gray'
                    aria-label={t('saves.action.download')}
                    component='a'
                    target='__blank'
                    href={getApiFullUrl(getSaveInfosDownloadUrl(save.id))}
                  >
                    <DownloadIcon />
                  </ActionIcon>
                </Tooltip>

                <UIConfirmPopover
                  label={t('saves.action.delete')}
                  icon={<TriangleAlertIcon />}
                  color='red'
                  action={() => saveDeleteMutation.mutateAsync({
                    params: { path: save.path },
                  })}
                >
                  <ActionIcon
                    disabled={!settings?.canUpdateSettings}
                    variant='subtle'
                    color='red'
                    aria-label={t('saves.action.delete')}
                  >
                    <TrashIcon />
                  </ActionIcon>
                </UIConfirmPopover>
              </>}
          />)}
        </UISavesSection>;
      })}
    </UISavesScroll>
  </UISavesContent>;
});
