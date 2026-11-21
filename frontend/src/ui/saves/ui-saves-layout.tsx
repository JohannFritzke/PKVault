import { Badge, Title } from '@mantine/core';
import type React from 'react';
import classes from './ui-saves-layout.module.css';

export const UISavesScroll: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    return <div className={classes.scroll}>
        <div className={classes.inner}>
            {children}
        </div>
    </div>;
};

export const UISavesHeader: React.FC<{ title: React.ReactNode; actions?: React.ReactNode }> = ({ title, actions }) => {
    return <div className={classes.header}>
        <div className={classes.headerTitle}>
            <Title order={1} fz={26} lh='32px' fw={600}>{title}</Title>
        </div>

        {actions && <div className={classes.headerActions}>
            {actions}
        </div>}
    </div>;
};

type UISavesSectionProps = {
    title: React.ReactNode;
    regions: string[];
    count: React.ReactNode;
    children: React.ReactNode;
};

export const UISavesSection: React.FC<UISavesSectionProps> = ({ title, regions, count, children }) => {
    return <section className={classes.section}>
        <div className={classes.sectionTitle}>
            <span>{title}</span>
            {regions.map(region => <Badge key={region} variant='outline' color='gray' size='sm' radius='xl'>{region}</Badge>)}
            <span className={classes.sectionCount}>{count}</span>
        </div>

        <div className={classes.cards}>
            {children}
        </div>
    </section>;
};
