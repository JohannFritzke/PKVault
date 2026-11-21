import { Button, Tabs } from '@mantine/core';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { DatabaseZapIcon, FileArchiveIcon, FileIcon, FolderIcon, FolderTreeIcon, GlobeIcon, PenOffIcon, RefreshCcwIcon, SaveIcon, ShieldOff } from 'lucide-react';
import { DataActionType } from '../../../data/sdk/model';
import { UIActionsPanel } from '../../actions-panel/ui-actions-panel';
import { UIGlobsInputItem } from '../../form/globs-input/ui-globs-input-item';
import { UIGlobsInputList } from '../../form/globs-input/ui-globs-input-list';
import { UIGlobsInputResults } from '../../form/globs-input/ui-globs-input-results';
import { UISelect } from '../../form/select/ui-select';
import { UISwitch } from '../../form/switch/ui-switch';
import { UITextInput } from '../../form/text-input/ui-text-input';
import { UIBallIcon } from '../../icon/ui-ball-icon';
import { UIAppLayout } from '../../layout/app-layout/ui-app-layout';
import { UIFooter } from '../../layout/footer/ui-footer';
import { UIHeader } from '../../layout/header/ui-header';
import { WithSettingsCategories as UIHeaderSettingsStory } from '../../layout/header/ui-header.stories';
import { UIBackupItem } from '../../settings/backups/ui-backup-item';
import { UIBackupList } from '../../settings/backups/ui-backup-list';
import { UIBackupsTabList } from '../../settings/backups/ui-backups-tab-list';
import { UISettingsRow } from '../../settings/section/ui-settings-row';
import { UISettingsSection } from '../../settings/section/ui-settings-section';
import { UISettingsContent } from '../../settings/ui-settings-content';
import { UIPathButton } from '../../form/globs-input/ui-path-button';
import { UIFileExplorerPopover } from '../../form/globs-input/ui-file-explorer-popover';

const meta = {
    title: 'Pages/Settings',
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

const generatePaths = (base: string, length: number) => new Array(length).fill(0).map((_, i) => `${base}-${i}.sav`);

export const Primary: Story = {
    render: () => <UIAppLayout
        header={<UIHeader
            {...UIHeaderSettingsStory.args}
        />}
        bottom={<UIActionsPanel
            data={[
                {
                    type: DataActionType.DATA_NORMALIZE,
                    description: 'Description',
                    label: null,
                    index: 0,
                },
                {
                    type: DataActionType.MOVE_PKM,
                    description: 'Description',
                    label: null,
                    index: 1,
                },
                {
                    type: DataActionType.MAIN_CREATE_BOX,
                    description: 'Description',
                    label: null,
                    index: 2,
                },
                {
                    type: DataActionType.EVOLVE_PKM,
                    description: 'Description',
                    label: null,
                    index: 3,
                },
                {
                    type: DataActionType.MAIN_DELETE_BANK,
                    description: 'Description',
                    label: null,
                    index: 4,
                },
            ]}
            onDelete={index => Promise.resolve(console.log(index))}
            onSave={() => Promise.resolve(console.log('save'))}
        />}
        footer={<UIFooter />}
        children={<UISettingsContent
            title='General'
            description='Application settings'
            children={<>
                <UISettingsSection>
                    <UISettingsRow icon={<GlobeIcon />} forInput='language' label='Language'>
                        <UISelect
                            name='language'
                            controlLabel='Change language'
                            value='english'
                            data={[
                                {
                                    value: 'english',
                                    label: 'English',
                                },
                                {
                                    value: 'francais',
                                    label: 'Français',
                                },
                                {
                                    value: 'foobar',
                                    label: 'Foobar',
                                },
                            ]}
                            onChange={console.log}
                            w={220}
                        />
                    </UISettingsRow>

                    <UISettingsRow icon={<PenOffIcon />} forInput='hide-cheats' label='Hide cheats' description='Hide settings allowing illegal pokemons'>
                        <UISwitch
                            name='hide-cheats'
                            controlLabel='Hide cheats'
                        />
                    </UISettingsRow>

                    <UISettingsRow icon={<ShieldOff />} forInput='skip-legality' label='Skip legality checks'>
                        <UISwitch
                            name='skip-legality'
                            controlLabel='Skip legality checks'
                        />
                    </UISettingsRow>
                </UISettingsSection>

                <UISettingsSection padded>
                    <UIGlobsInputList
                        id='saves'
                        labelList='Saves files locations'
                        labelAddFile='Add a save'
                        labelAddFolder='Add a save directory'
                        labelAddPath='Add saves location'
                        onAdd={async (...params) => console.log(...params)}
                        canUseDesktopFileExplorer//={false}
                        results={<UIGlobsInputResults
                            name='results'
                            data={generatePaths('c:/abc/def/', 100)}
                            showFiles
                        />}
                    >
                        <UIGlobsInputItem
                            name='1'
                            value={'./**/*.sav'}
                            onRemove={() => console.log('remove')}
                            // disabled={disabled}
                            results={generatePaths('c:/abc/zoo', 100)}
                        >
                            {props => <UIFileExplorerPopover
                                name='1-explorer'
                                value={'./**/*.sav'}
                                onChange={console.log}
                                reloadData={console.log}
                                // loading
                                dataDirectoryPaths={[ './foo', './bar' ]}
                                dataFilePaths={[ './toto.txt', './tata.sav' ]}
                                pkvaultPath=''
                                uploadPath=''
                                setDropdownOpened={console.log}
                            >
                                {otherProps => <UIPathButton
                                    {...props}
                                    {...otherProps}
                                    value={'./**/*.sav'}
                                    onClick={e => {
                                        props.onClick?.(e);
                                        otherProps.onClick?.(e);
                                    }}
                                    pkvaultPath=''
                                    uploadPath=''
                                />}
                            </UIFileExplorerPopover>}
                        </UIGlobsInputItem>

                        <UIGlobsInputItem
                            name='2'
                            value={'./foo/bar.bin'}
                            onRemove={() => console.log('remove')}
                            // disabled={disabled}
                            results={generatePaths('c:/abc/foo', 100)}
                        >
                            {props => <UIFileExplorerPopover
                                name='2-explorer'
                                value={'./**/*.sav'}
                                onChange={console.log}
                                reloadData={console.log}
                                // loading
                                dataDirectoryPaths={[ './foo', './bar' ]}
                                dataFilePaths={[ './toto.txt', './tata.sav' ]}
                                pkvaultPath=''
                                uploadPath=''
                                setDropdownOpened={console.log}
                            >
                                {otherProps => <UIPathButton
                                    {...props}
                                    {...otherProps}
                                    value={'./**/*.sav'}
                                    onClick={e => {
                                        props.onClick?.(e);
                                        otherProps.onClick?.(e);
                                    }}
                                    pkvaultPath=''
                                    uploadPath=''
                                />}
                            </UIFileExplorerPopover>}
                        </UIGlobsInputItem>

                        <UIGlobsInputItem
                            name='3'
                            value={'!./**/*.bin'}
                            onRemove={() => console.log('remove')}
                            // disabled={disabled}
                            results={generatePaths('c:/abc/foo', 100)}
                        >
                            {props => <UIFileExplorerPopover
                                name='3-explorer'
                                value={'./**/*.sav'}
                                onChange={console.log}
                                reloadData={console.log}
                                // loading
                                dataDirectoryPaths={[ './foo', './bar' ]}
                                dataFilePaths={[ './toto.txt', './tata.sav' ]}
                                pkvaultPath=''
                                uploadPath=''
                                setDropdownOpened={console.log}
                            >
                                {otherProps => <UIPathButton
                                    {...props}
                                    {...otherProps}
                                    value={'./**/*.sav'}
                                    onClick={e => {
                                        props.onClick?.(e);
                                        otherProps.onClick?.(e);
                                    }}
                                    pkvaultPath=''
                                    uploadPath=''
                                />}
                            </UIFileExplorerPopover>}
                        </UIGlobsInputItem>
                    </UIGlobsInputList>
                </UISettingsSection>
                <UISettingsSection>
                    <UISettingsRow icon={<UIBallIcon />} label='PKVault'>v2.0.0 - SteamDeck - Flatpak</UISettingsRow>
                    <UISettingsRow icon={<img src="https://projectpokemon.org/favicon.ico" />} label='PKHeX'>26.02.26</UISettingsRow>
                    <UISettingsRow icon={<FolderIcon />} label='PKVault path'>/pkvault</UISettingsRow>
                    <UISettingsRow icon={<FileIcon />} label='Config path'>/pkvault/config/pkvault.json</UISettingsRow>
                </UISettingsSection>
            </>}
            bottom={<>
                <Button>
                    Cancel
                </Button>

                <Button
                    variant='filled'
                    leftSection={<SaveIcon />}
                >
                    Save
                </Button>
            </>}
        />}
    />,
};

export const Backups: Story = {
    render: () => <UIAppLayout
        header={<UIHeader
            {...UIHeaderSettingsStory.args}
        />}
        bottom={<UIActionsPanel
            data={[
                {
                    type: DataActionType.DATA_NORMALIZE,
                    description: 'Description',
                    label: null,
                    index: 0,
                },
                {
                    type: DataActionType.MOVE_PKM,
                    description: 'Description',
                    label: null,
                    index: 1,
                },
                {
                    type: DataActionType.MAIN_CREATE_BOX,
                    description: 'Description',
                    label: null,
                    index: 2,
                },
                {
                    type: DataActionType.EVOLVE_PKM,
                    description: 'Description',
                    label: null,
                    index: 3,
                },
                {
                    type: DataActionType.MAIN_DELETE_BANK,
                    description: 'Description',
                    label: null,
                    index: 4,
                },
            ]}
            onDelete={index => Promise.resolve(console.log(index))}
            onSave={() => Promise.resolve(console.log('save'))}
        />}
        footer={<UIFooter />}
        children={<UISettingsContent
            title='Backups'
            description='PKVault creates a backup before each save'
            children={<>
            <UIBackupList
                title='Local backups'
                description='/pkvault/backups'
                actions={<Button variant='filled' leftSection={<DatabaseZapIcon />}>
                    Create backup
                </Button>}
                tabs={<UIBackupsTabList
                    value='26/02/2026'
                    onSelect={console.log}
                    scopeId='storage-content'
                >
                    <Tabs.Tab value='26/02/2026' py='sm' px='md'>
                        26/02/2026
                    </Tabs.Tab>
                    <Tabs.Tab value='25/02/2026' py='sm' px='md'>
                        25/02/2026
                    </Tabs.Tab>
                </UIBackupsTabList>}
            >
                {new Array(15).fill(0).map((_, i) => <UIBackupItem
                    key={i}
                    order={i}
                    createdAt={`2026-05-25T14:35:${i < 10 ? 0 : ''}${i}.496+00:00`}
                    filename={`backup_before_save_bkp_2026-04-25T1259${i < 10 ? 0 : ''}${i}-832Z.zip`}
                    path={`C:/foo/bar/backups/backup_before_save_bkp_2026-04-25T1259${i < 10 ? 0 : ''}${i}-832Z.zip`}
                    onRestore={() => console.log('restore', i)}
                    onDelete={() => console.log('delete', i)}
                >
                    <UITextInput
                        name={`2026-05-25T14:35:${i < 10 ? 0 : ''}${i}.496+00:00-input`}
                        value={'backup_before_save'}
                        onChange={console.log}
                        // maw={200}
                        styles={{
                            input: {
                                height: 'auto',
                                minHeight: 0,
                                lineHeight: 'inherit',
                            },
                        }}
                        onSubmit={console.log}
                        onCancel={console.log}
                    />
                </UIBackupItem>)}
            </UIBackupList>

            <UISettingsSection>
                <UISettingsRow icon={<FolderTreeIcon />} label='Backup content' description='All saves + all storage' />
                <UISettingsRow icon={<RefreshCcwIcon />} label='Auto-backup' description='Before any file write (save)' />
                <UISettingsRow icon={<FileArchiveIcon />} label='Generic format' description='Can be opened from outside PKVault (.zip)' />
            </UISettingsSection>
            </>}
            bottom={<>
                <Button>
                    Cancel
                </Button>

                <Button
                    variant='filled'
                    leftSection={<SaveIcon />}
                >
                    Save
                </Button>
            </>}
        />}
    />,
};
