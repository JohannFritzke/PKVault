import { Table } from '@mantine/core';
import type React from 'react';
import { UISettingsSection } from '../section/ui-settings-section';

type UIBackupListProps = {
    title: React.ReactNode;
    description?: React.ReactNode;
    actions?: React.ReactNode;
    tabs: React.ReactNode;
    empty?: React.ReactNode;
    children: React.ReactNode;
};

export const UIBackupList: React.FC<UIBackupListProps> = ({ title, description, actions, tabs, empty, children }) => {
    return <UISettingsSection
        title={title}
        description={description}
        actions={actions}
    >
        {tabs}

        {empty}

        <div style={{ overflowX: 'auto' }}>
            <Table horizontalSpacing='lg' verticalSpacing='sm' fz='sm'>
                <Table.Tbody>
                    {children}
                </Table.Tbody>
            </Table>
        </div>
    </UISettingsSection>;
};
