import { Alert, Badge, EmptyState, Group, Loader } from '@mantine/core';
import { ArrowLeftIcon, CirclePlusIcon, FolderOpenIcon, PackageOpenIcon, TriangleAlertIcon } from 'lucide-react';
import React from 'react';
import { DesktopMessageType, FolderScanRefusal, type DiscoveredSaveDTO, type SaveDiscoveryResultDTO } from '../data/sdk/model';
import { useSaveDiscoveryAdd, useSaveDiscoveryDiscover, useSaveDiscoveryScan } from '../data/sdk/save-discovery/save-discovery.gen';
import { withErrorCatcher } from '../error/with-error-catcher';
import { useStaticData } from '../hooks/use-static-data';
import { getGameInfos } from '../pokedex/details/util/get-game-infos';
import { Route } from '../routes/saves_.discovery';
import { useDesktopMessage } from '../settings/globs-input/hooks/use-desktop-message';
import { useTranslate } from '../translate/i18n';
import { UIButton } from '../ui/form/button/ui-button';
import { getDesktopFileTypeInfos } from '../ui/form/globs-input/util/get-desktop-file-type-infos';
import { UITextInput } from '../ui/form/text-input/ui-text-input';
import { UIDiscoveredSave } from '../ui/saves/discovery/ui-discovered-save';
import discoveredClasses from '../ui/saves/discovery/ui-discovered-save.module.css';
import { UISavesContent } from '../ui/saves/ui-saves-content';
import { UISavesHeader, UISavesScroll, UISavesSection } from '../ui/saves/ui-saves-layout';

const refusalKeys = {
    [ FolderScanRefusal.INVALID_PATH ]: 'discovery.refusal.invalid',
    [ FolderScanRefusal.NOT_FOUND ]: 'discovery.refusal.not-found',
    [ FolderScanRefusal.DRIVE_ROOT ]: 'discovery.refusal.drive-root',
    [ FolderScanRefusal.SYSTEM_FOLDER ]: 'discovery.refusal.system',
    [ FolderScanRefusal.HOME_FOLDER ]: 'discovery.refusal.home',
} as const;

/** Merges results by path; later results win (a folder scan refreshes what auto-discovery found). */
const mergeSaves = (...results: (SaveDiscoveryResultDTO | undefined)[]) => {
    const byPath = new Map<string, DiscoveredSaveDTO>();
    results.forEach(result => result?.saves.forEach(save => byPath.set(save.path, save)));
    return [ ...byPath.values() ];
};

export const SaveDiscoveryPage: React.FC = withErrorCatcher('default', () => {
    const { t } = useTranslate();
    const navigate = Route.useNavigate();
    const staticData = useStaticData();
    const desktopMessage = useDesktopMessage();

    const discoverQuery = useSaveDiscoveryDiscover({ emulatorFolders: [] });
    const scanMutation = useSaveDiscoveryScan();
    const addMutation = useSaveDiscoveryAdd();

    const [ folder, setFolder ] = React.useState('');
    const [ scanResults, setScanResults ] = React.useState<SaveDiscoveryResultDTO[]>([]);
    const [ lastScan, setLastScan ] = React.useState<SaveDiscoveryResultDTO>();

    const discovered = discoverQuery.data?.data;
    const saves = mergeSaves(discovered, ...scanResults)
        .sort((a, b) => a.generation - b.generation || a.version - b.version || a.path.localeCompare(b.path));

    const isSelectable = (save: DiscoveredSaveDTO) => !save.alreadyAdded;

    // selection defaults to every new, non-duplicate save; user changes are kept per path
    const [ overrides, setOverrides ] = React.useState<Record<string, boolean>>({});
    const isChecked = (save: DiscoveredSaveDTO) => isSelectable(save)
        && (overrides[ save.path ] ?? !save.isDuplicateOf);

    const selectedPaths = saves.filter(isChecked).map(save => save.path);

    const scan = async (path: string) => {
        const trimmed = path.trim();
        if (!trimmed)
            return;

        const response = await scanMutation.mutateAsync({ data: { path: trimmed } });
        setLastScan(response.data);
        setScanResults(results => [ ...results, response.data ]);
    };

    const pickFolder = desktopMessage.fileExplore && (async () => {
        const infos = getDesktopFileTypeInfos('folder');
        const response = await desktopMessage.fileExplore!({
            type: DesktopMessageType.FILE_EXPLORE,
            id: infos.id,
            directoryOnly: infos.directoryOnly,
            basePath: folder,
            multiselect: false,
        });
        const value = infos.getFinalPaths(response?.values ?? [])[ 0 ];
        if (!value)
            return;

        setFolder(value);
        await scan(value);
    });

    const addSelected = async () => {
        await addMutation.mutateAsync({ data: { paths: selectedPaths } });
        await navigate({ to: '/saves' });
    };

    const emulators = discovered?.emulators ?? [];
    const truncated = !!discovered?.truncated || scanResults.some(result => result.truncated);
    const refusal = lastScan?.refusal;
    const isLoading = discoverQuery.isPending && discoverQuery.isEnabled;

    const generations = [ ...new Set(saves.map(save => save.generation)) ];

    return <UISavesContent>
        <UISavesScroll>
            <UISavesHeader
                title={t('discovery.title')}
                description={t('discovery.subtitle')}
                actions={<>
                    <UIButton
                        name='discovery-back'
                        controlLabel={t('action.back')}
                        onClick={() => navigate({ to: '/saves' })}
                        leftSection={<ArrowLeftIcon />}
                        variant='default'
                    >
                        {t('action.back')}
                    </UIButton>

                    <UIButton
                        name='discovery-add'
                        controlLabel={t('discovery.add', { count: selectedPaths.length })}
                        onClick={addSelected}
                        disabled={selectedPaths.length === 0}
                        loading={addMutation.isPending}
                        leftSection={<CirclePlusIcon />}
                        variant='filled'
                    >
                        {t('discovery.add', { count: selectedPaths.length })}
                    </UIButton>
                </>}
            />

            <Group gap='sm' wrap='nowrap' align='flex-end' maw={720} style={{ width: '100%' }}>
                <div className={discoveredClasses.folderField}>
                <UITextInput
                    name='discovery-folder'
                    label={t('discovery.folder.label')}
                    placeholder={t('discovery.folder.placeholder')}
                    value={folder}
                    onChange={e => setFolder(e.currentTarget.value)}
                    onSubmit={() => scan(folder)}
                    submitDisabled={!folder.trim()}
                    submitLoading={scanMutation.isPending}
                />
                </div>

                {pickFolder && <UIButton
                    name='discovery-browse'
                    controlLabel={t('discovery.folder.browse')}
                    onClick={pickFolder}
                    leftSection={<FolderOpenIcon />}
                    variant='default'
                >
                    {t('discovery.folder.browse')}
                </UIButton>}
            </Group>

            {refusal !== undefined && <Alert variant='outline' color='yellow' icon={<TriangleAlertIcon />}>
                {t(refusalKeys[ refusal ])}
            </Alert>}

            {truncated && <Alert variant='outline' color='yellow' icon={<TriangleAlertIcon />}>
                {t('discovery.truncated')}
            </Alert>}

            {emulators.length > 0 && <Group gap='sm'>
                <span style={{ fontSize: 13, color: 'var(--mantine-color-dimmed)' }}>{t('discovery.emulators')}</span>
                {emulators.map(emulator => <Badge key={emulator.installPath} variant='default' size='lg' title={emulator.installPath}>
                    {emulator.name}
                    {emulator.portable && ` (${t('discovery.emulator.portable')})`}
                    {' · '}
                    {t('discovery.emulator.folders', { count: emulator.saveFolders.length })}
                </Badge>)}
            </Group>}

            {isLoading && <Group justify='center' py='xl'><Loader size='sm' /></Group>}

            {!isLoading && saves.length === 0 && <EmptyState
                size='sm'
                py='xl'
                icon={<PackageOpenIcon />}
                title={t('discovery.empty')}
            />}

            {generations.map(generation => <UISavesSection
                key={generation}
                title={t('saves.title', { generation })}
                regions={staticData.generations[ generation ]?.regions ?? []}
                count={null}
            >
                {saves
                    .filter(save => save.generation === generation)
                    .map(save => <UIDiscoveredSave
                        key={save.path}
                        path={save.path}
                        label={staticData.versions[ save.version ]?.name ?? save.version}
                        imgSrc={getGameInfos(save.version).img}
                        generation={`G${save.generation}`}
                        trainerName={save.trainerName}
                        tid={save.tid}
                        playTime={save.playTime}
                        source={emulators.find(emulator => emulator.emulatorId === save.emulatorId)?.name}
                        checked={isChecked(save)}
                        alreadyAdded={save.alreadyAdded}
                        duplicateOf={save.isDuplicateOf}
                        onToggle={() => setOverrides(value => ({ ...value, [ save.path ]: !isChecked(save) }))}
                    />)}
            </UISavesSection>)}
        </UISavesScroll>
    </UISavesContent>;
});
