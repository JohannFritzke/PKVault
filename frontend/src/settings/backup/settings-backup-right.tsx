import { EmptyState, Tabs, Tooltip, useMatches } from '@mantine/core';
import { DatabaseZapIcon, PackageOpenIcon } from 'lucide-react';
import React from "react";
import { useBackupCreate, useBackupDelete, useBackupGetAll, useBackupRestore } from '../../data/sdk/backup/backup.gen';
import type { BackupDTO } from '../../data/sdk/model';
import { useSettingsGet } from '../../data/sdk/settings/settings.gen';
import { useTranslate } from '../../translate/i18n';
import { UIButton } from '../../ui/form/button/ui-button';
import { PathUtil } from '../../ui/form/globs-input/util/path-util';
import { UIPathLine } from '../../ui/path/ui-path-line';
import { UIBackupItem } from '../../ui/settings/backups/ui-backup-item';
import { UIBackupList } from '../../ui/settings/backups/ui-backup-list';
import { UIBackupsTabList } from '../../ui/settings/backups/ui-backups-tab-list';
import classes from '../../ui/settings/ui-settings.module.css';
import { renderDate, renderTime } from '../../util/render-date-time';
import { BackupLineForm } from './backup-line-form';

export const SettingsBackupRight: React.FC = () => {
    const { t } = useTranslate();

    const settingsQuery = useSettingsGet();
    const settings = settingsQuery.data?.data;

    const backupQuery = useBackupGetAll();

    const backupCreateMutation = useBackupCreate();
    const backupRestoreMutation = useBackupRestore();
    const backupDeleteMutation = useBackupDelete();

    const sortedBackups = [ ...backupQuery.data?.data ?? [] ]
        .sort((a, b) => a.createdAt < b.createdAt ? 1 : -1)
        .map(backup => {
            const createdAtDate = new Date(backup.createdAt);

            return {
                backup,
                createdAtDateStr: renderDate(createdAtDate),
                createdAtTimeStr: renderTime(createdAtDate),
            };
        });

    const days = [ ...new Set(sortedBackups.map(backup => backup.createdAtDateStr)) ];

    const [ selectedDay, setSelectedDay ] = React.useState(days[ 0 ]);

    const getItemChildren = useMatches({
        base: (_: BackupDTO): React.ReactNode => null,
        sm: (backup: BackupDTO) => <BackupLineForm
            createdAt={backup.createdAt}
            name={backup.name}
            disabled={settings?.demoMode}
        />,
    });

    return <UIBackupList
        title={t('settings.backups.1.label')}
        description={<UIPathLine className={classes.mono}>{settings?.settingsMutable.backuP_PATH ?? '-'}</UIPathLine>}
        actions={<Tooltip
            label={t('action.not-possible')}
            disabled={settings?.canCreateBackup}
        >
            <UIButton
                name='create-backup'
                controlLabel={t('action.select')}
                onClick={() => backupCreateMutation.mutateAsync()}
                disabled={!settings?.canCreateBackup}
                loading={backupCreateMutation.isPending}
                variant='filled'
                leftSection={<DatabaseZapIcon />}
            >
                {t('settings.backups.create')}
            </UIButton>
        </Tooltip>}
        tabs={<UIBackupsTabList
            value={selectedDay ?? ''}
            onSelect={setSelectedDay}
            scopeId='settings-content'
        >
            {days.length === 0 && <Tabs.Tab value='-' py='sm' style={{
                opacity: 0,
                pointerEvents: 'none',
            }}>
            </Tabs.Tab>}

            {days.map(day => <Tabs.Tab key={day} value={day} py='sm' px='md'>
                {day}
            </Tabs.Tab>)}
        </UIBackupsTabList>}
        empty={sortedBackups.length === 0 && <EmptyState
            size='sm'
            py='xl'
            icon={<PackageOpenIcon />}
            title={t('settings.form.saves.empty')}
        />}
    >
        {sortedBackups
            .filter(bkp => bkp.createdAtDateStr === selectedDay)
            .map(({ backup }, i) => <UIBackupItem
                key={backup.filepath}
                order={i}
                createdAt={backup.createdAt}
                filename={PathUtil.getFileName(backup.filepath)}
                path={backup.filepath}
                onRestore={() => backupRestoreMutation.mutateAsync({ params: { createdAt: backup.createdAt } })}
                onDelete={() => backupDeleteMutation.mutateAsync({ params: { createdAt: backup.createdAt } })}
                disabled={settings?.demoMode}
            >
                {getItemChildren(backup)}
            </UIBackupItem>)}
    </UIBackupList>;
};
