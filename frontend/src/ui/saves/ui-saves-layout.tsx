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

export const UISavesHeader: React.FC<{ title: React.ReactNode; description?: React.ReactNode; actions?: React.ReactNode }> = ({ title, description, actions }) => {
    return <div className={classes.header}>
        <div className={classes.headerTitle}>
            <Title order={1} fz={24} lh='30px' fw={600}>{title}</Title>
            {description && <div className={classes.headerDescription}>{description}</div>}
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
            {regions.map(region => <Badge key={region} variant='default' size='sm'>{region}</Badge>)}
            <span className={classes.sectionCount}>{count}</span>
            <span className={classes.sectionRule} />
        </div>

        <div className={classes.cards}>
            {children}
        </div>
    </section>;
};
